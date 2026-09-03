using System.Collections.ObjectModel;
using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.Services;

public class JobManager : IJobManager
{
    private readonly IAudioAnalyzer _audioAnalyzer;
    private readonly IFFprobeService _probeService;
    private readonly ISceneDetector? _sceneDetector;
    private readonly ITimelineBuilder _timelineBuilder;
    private readonly IVideoRenderer _videoRenderer;
    private readonly ISettingsService _settingsService;
    private readonly ITempFileManager _tempFileManager;
    private readonly ILogService _logService;
    private readonly ILogger<JobManager>? _logger;

    private readonly ObservableCollection<VideoJob> _jobs = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _jobCts = new();
    private readonly object _lock = new();

    private CancellationTokenSource? _globalCts;
    private bool _isProcessing;
    private bool _isPaused;

    public ObservableCollection<VideoJob> Jobs => _jobs;
    public object SyncRoot => _lock;
    public bool IsProcessing => _isProcessing;
    public bool IsPaused => _isPaused;

    public event Action<VideoJob>? JobStatusChanged;
    public event Action<VideoJob, JobProgressReport>? JobProgressUpdated;
    public event Action? QueueFinished;

    public JobManager(
        IAudioAnalyzer audioAnalyzer,
        IFFprobeService probeService,
        ITimelineBuilder timelineBuilder,
        IVideoRenderer videoRenderer,
        ISettingsService settingsService,
        ITempFileManager tempFileManager,
        ILogService logService,
        ISceneDetector? sceneDetector = null,
        ILogger<JobManager>? logger = null)
    {
        _audioAnalyzer = audioAnalyzer;
        _probeService = probeService;
        _sceneDetector = sceneDetector;
        _timelineBuilder = timelineBuilder;
        _videoRenderer = videoRenderer;
        _settingsService = settingsService;
        _tempFileManager = tempFileManager;
        _logService = logService;
        _logger = logger;
    }

    public void AddJob(VideoJob job)
    {
        lock (_lock)
        {
            job.OrderIndex = _jobs.Count + 1;
            _jobs.Add(job);
        }
        var tag = $"[JOB {job.OrderIndex:D3}]";
        _logService.LogInfo($"Đã thêm công việc: {job.VideoFileName} + {job.VoiceFileName}", job.Id.ToString(), tag);
    }

    public void AddJobs(IEnumerable<VideoJob> jobs)
    {
        var jobList = jobs.ToList();
        lock (_lock)
        {
            foreach (var job in jobList)
            {
                job.OrderIndex = _jobs.Count + 1;
                _jobs.Add(job);
            }
        }
        _logService.LogInfo($"Đã thêm {jobList.Count} công việc vào hàng đợi.");
    }

    public void RemoveJob(Guid jobId)
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job != null)
            {
                CancelJobInternal(jobId);
                _jobs.Remove(job);
                _tempFileManager.CleanupJobTempDirectory(jobId);
            }
        }
    }

    public void ClearAllJobs()
    {
        lock (_lock)
        {
            CancelAllInternal();
            foreach (var job in _jobs)
            {
                _tempFileManager.CleanupJobTempDirectory(job.Id);
            }
            _jobs.Clear();
        }
        _logService.LogInfo("Đã xóa toàn bộ danh sách công việc trong hàng đợi.");
    }

    public void ClearCompletedJobs()
    {
        lock (_lock)
        {
            var completed = _jobs.Where(j => j.Status == JobStatus.Completed).ToList();
            foreach (var job in completed)
            {
                _jobs.Remove(job);
                _tempFileManager.CleanupJobTempDirectory(job.Id);
            }
        }
        _logService.LogInfo("Đã dọn dẹp các công việc đã hoàn thành.");
    }

    public async Task StartQueueAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isProcessing && !_isPaused)
                return;

            _isProcessing = true;
            _isPaused = false;
            _globalCts?.Dispose();
            _globalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        var settings = _settingsService.CurrentSettings;
        var maxParallel = Math.Clamp(settings.MaxParallelJobs, 1, 16);
        using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);

        _logService.LogInfo($"Bắt đầu xử lý hàng đợi. Số luồng đồng thời: {maxParallel}");

        var runningTasks = new List<Task>();

        while (!_globalCts.Token.IsCancellationRequested)
        {
            if (_isPaused)
            {
                await Task.Delay(300, _globalCts.Token).ConfigureAwait(false);
                continue;
            }

            VideoJob? nextJob = null;
            lock (_lock)
            {
                nextJob = _jobs.FirstOrDefault(j => j.Status == JobStatus.Pending);
                if (nextJob != null)
                {
                    nextJob.Status = JobStatus.AnalyzingVideo;
                    nextJob.CurrentStepDescription = "Đang bắt đầu...";
                }
            }

            if (nextJob == null)
            {
                break;
            }

            JobStatusChanged?.Invoke(nextJob);
            await semaphore.WaitAsync(_globalCts.Token).ConfigureAwait(false);

            var jobToRun = nextJob;
            var jobCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
            lock (_lock)
            {
                _jobCts[jobToRun.Id] = jobCts;
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    await ProcessSingleJobAsync(jobToRun, jobCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    lock (_lock)
                    {
                        _jobCts.Remove(jobToRun.Id);
                    }
                    semaphore.Release();
                }
            });

            runningTasks.Add(task);
        }

        try
        {
            await Task.WhenAll(runningTasks).ConfigureAwait(false);
        }
        catch
        {
            // Ignore cancellation exceptions from WhenAll
        }

        lock (_lock)
        {
            _isProcessing = false;
        }

        _logService.LogSuccess("Hàng đợi đã hoàn tất toàn bộ tiến trình xử lý video.");
        QueueFinished?.Invoke();
    }

    public Task PauseQueueAsync()
    {
        _isPaused = true;
        _logService.LogWarning("Đã tạm dừng hàng đợi. Các job đang chạy sẽ hoàn thành, job mới sẽ chờ.");
        return Task.CompletedTask;
    }

    public Task ResumeQueueAsync()
    {
        _isPaused = false;
        _logService.LogInfo("Tiếp tục xử lý hàng đợi.");
        return Task.CompletedTask;
    }

    public Task CancelJobAsync(Guid jobId)
    {
        CancelJobInternal(jobId);
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        CancelAllInternal();
        lock (_lock)
        {
            _isProcessing = false;
            _isPaused = false;
        }
        _logService.LogWarning("Đã hủy toàn bộ hàng đợi xử lý.");
        return Task.CompletedTask;
    }

    public async Task RetryFailedJobsAsync()
    {
        lock (_lock)
        {
            var failedJobs = _jobs.Where(j => j.Status == JobStatus.Failed || j.Status == JobStatus.Canceled).ToList();
            foreach (var job in failedJobs)
            {
                job.Status = JobStatus.Pending;
                job.ProgressPercentage = 0;
                job.CurrentStepDescription = "Đang chờ";
                job.ErrorMessage = null;
            }
        }

        _logService.LogInfo("Đã đưa các công việc lỗi/đã hủy về trạng thái Đang chờ để thử lại.");

        if (!_isProcessing)
        {
            _ = Task.Run(() => StartQueueAsync());
        }
    }

    private void CancelJobInternal(Guid jobId)
    {
        lock (_lock)
        {
            if (_jobCts.TryGetValue(jobId, out var cts))
            {
                cts.Cancel();
                _jobCts.Remove(jobId);
            }

            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job != null && job.Status != JobStatus.Completed)
            {
                job.Status = JobStatus.Canceled;
                job.CurrentStepDescription = "Đã hủy";
                JobStatusChanged?.Invoke(job);
                _logService.LogWarning($"Công việc đã bị hủy.", jobId.ToString(), $"[JOB {job.OrderIndex:D3}]");
            }
        }
    }

    private void CancelAllInternal()
    {
        _globalCts?.Cancel();
        lock (_lock)
        {
            foreach (var kvp in _jobCts)
            {
                kvp.Value.Cancel();
            }
            _jobCts.Clear();

            foreach (var job in _jobs.Where(j => j.Status == JobStatus.Pending || j.Status == JobStatus.AnalyzingVoice || j.Status == JobStatus.AnalyzingVideo || j.Status == JobStatus.BuildingTimeline || j.Status == JobStatus.Rendering))
            {
                job.Status = JobStatus.Canceled;
                job.CurrentStepDescription = "Đã hủy";
                JobStatusChanged?.Invoke(job);
            }
        }
    }

    private async Task ProcessSingleJobAsync(VideoJob job, CancellationToken ct)
    {
        job.StartedAt = DateTime.Now;
        var tag = $"[JOB {job.OrderIndex:D3}]";
        var jId = job.Id.ToString();

        try
        {
            _logService.LogInfo($"Bắt đầu xử lý...", jId, tag);
            _logService.LogInfo($"Video: {job.VideoFileName}", jId, tag);
            _logService.LogInfo($"Giọng đọc: {job.VoiceFileName}", jId, tag);

            // 1. Validate files
            if (job.VideoPaths.Count == 0 || !job.VideoPaths.All(File.Exists))
            {
                throw new FileNotFoundException("Một hoặc nhiều file video đầu vào không tồn tại.");
            }
            if (string.IsNullOrEmpty(job.VoicePath) || !File.Exists(job.VoicePath))
            {
                throw new FileNotFoundException("File giọng đọc không tồn tại.");
            }

            // 2. Video Analysis (FFprobe)
            UpdateJobState(job, JobStatus.AnalyzingVideo, "Đang phân tích video...", 5);
            _logService.LogInfo("Đang kiểm tra thông tin video qua FFprobe...", jId, tag);
            job.VideoMetadatas.Clear();
            foreach (var vPath in job.VideoPaths)
            {
                var vInfo = await _probeService.ProbeFileAsync(vPath, ct).ConfigureAwait(false);
                job.VideoMetadatas.Add(vInfo);
                _logService.LogInfo($"Video Info: {vInfo.Width}x{vInfo.Height}, {vInfo.Fps:F0} FPS, Thời lượng: {vInfo.DurationSeconds:F2}s, Codec: {vInfo.VideoCodec}", jId, tag);
            }

            // 3. Voice Analysis (Silence detection)
            UpdateJobState(job, JobStatus.AnalyzingVoice, "Đang phân tích giọng đọc...", 15);
            _logService.LogInfo("Đang phân tích giọng đọc & phát hiện khoảng lặng...", jId, tag);
            _logService.LogInfo($"Ngưỡng silence: {job.Preset.SilenceThresholdDb:F0} dB | Tối thiểu: {job.Preset.MinSilenceDurationMs} ms | Đệm: {job.Preset.PaddingBeforeMs} ms", jId, tag);

            job.VoiceAnalysis = await _audioAnalyzer.AnalyzeVoiceAsync(
                job.VoicePath,
                job.Preset.SilenceThresholdDb,
                job.Preset.MinSilenceDurationMs,
                job.Preset.PaddingBeforeMs,
                job.Preset.PaddingAfterMs,
                400,
                ct).ConfigureAwait(false);

            var vTrimStart = job.VoiceTrimStartSeconds > 0.001 ? job.VoiceTrimStartSeconds : Math.Max(0.0, job.Preset.VoiceTrimStartSeconds);
            var vTrimEnd = job.VoiceTrimEndSeconds > 0.001 ? job.VoiceTrimEndSeconds : Math.Max(0.0, job.Preset.VoiceTrimEndSeconds);
            var vidTrimStart = job.VideoTrimStartSeconds > 0.001 ? job.VideoTrimStartSeconds : Math.Max(0.0, job.Preset.VideoTrimStartSeconds);
            var vidTrimEnd = job.VideoTrimEndSeconds > 0.001 ? job.VideoTrimEndSeconds : Math.Max(0.0, job.Preset.VideoTrimEndSeconds);
            var extraEnd = job.ExtraEndPaddingSeconds > 0.001 ? job.ExtraEndPaddingSeconds : Math.Max(0.0, job.Preset.ExtraEndPaddingSeconds);

            if (vTrimStart > 0 || vTrimEnd > 0)
            {
                var maxEnd = Math.Max(vTrimStart + 0.1, job.VoiceAnalysis.OriginalDurationSeconds - vTrimEnd);
                var filteredSegments = new List<SpeechSegment>();
                int sIdx = 1;
                foreach (var seg in job.VoiceAnalysis.SpeechSegments)
                {
                    var clampedStart = Math.Max(vTrimStart, seg.StartSeconds);
                    var clampedEnd = Math.Min(maxEnd, seg.EndSeconds);
                    if (clampedEnd > clampedStart + 0.05)
                    {
                        filteredSegments.Add(new SpeechSegment(sIdx++, clampedStart, clampedEnd));
                    }
                }
                if (filteredSegments.Count > 0)
                {
                    job.VoiceAnalysis.SpeechSegments = filteredSegments;
                    job.VoiceAnalysis.ProcessedDurationSeconds = filteredSegments.Sum(s => s.DurationSeconds);
                }
                _logService.LogInfo($"Đã áp dụng cắt giọng đọc: -{vTrimStart:F1}s đầu, -{vTrimEnd:F1}s cuối.", jId, tag);
            }

            _logService.LogInfo($"Thời lượng voice gốc: {job.VoiceAnalysis.OriginalDurationSeconds:F2}s", jId, tag);
            _logService.LogInfo($"Phát hiện: {job.VoiceAnalysis.SilenceSegments.Count} đoạn khoảng lặng. Tổng đã cắt: {job.VoiceAnalysis.SilenceDurationRemovedSeconds:F2}s ({job.VoiceAnalysis.SilenceRemovalPercentage:F1}%)", jId, tag);
            _logService.LogSuccess($"Thời lượng voice sau xử lý: {job.VoiceAnalysis.ProcessedDurationSeconds:F2}s", jId, tag);
            if (extraEnd > 0)
            {
                _logService.LogInfo($"Thêm dư cuối video (Outro hold): +{extraEnd:F1}s | Tổng thời lượng video: {(job.VoiceAnalysis.ProcessedDurationSeconds + extraEnd):F2}s", jId, tag);
            }

            // 4. Scene Detection & Timeline Plan
            UpdateJobState(job, JobStatus.BuildingTimeline, "Đang xây dựng timeline...", 25);
            _logService.LogInfo("Đang xây dựng timeline video theo thời lượng voice...", jId, tag);
            if (vidTrimStart > 0 || vidTrimEnd > 0)
            {
                _logService.LogInfo($"Cắt bỏ video riêng cho job: -{vidTrimStart:F1}s đầu, -{vidTrimEnd:F1}s cuối.", jId, tag);
            }

            // Detect natural scenes if available
            if (job.VideoPaths.Count == 1 && job.Preset.EnableSmartSceneCut && _sceneDetector != null)
            {
                try
                {
                    job.DetectedScenes = await _sceneDetector.DetectScenesAsync(job.VideoPaths[0], 0.3, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Scene detection failed, using rhythmic dynamic scenes.");
                }
            }

            var reqTransCount = job.CustomTransitionCount ?? job.Preset.TransitionCount;
            var reqTransType = job.CustomTransitionType ?? job.Preset.TransitionType;

            job.TimelinePlan = _timelineBuilder.BuildTimeline(
                job.VideoMetadatas,
                job.VoiceAnalysis,
                job.Preset,
                vidTrimStart,
                vidTrimEnd,
                extraEnd,
                reqTransCount,
                reqTransType,
                job.DetectedScenes);

            job.PlannedTransitions = job.TimelinePlan.Transitions;

            // Log scenes and transitions in requested user format
            int totalScenes = job.TimelinePlan.Scenes.Count;
            int validCutPoints = Math.Max(0, totalScenes - 1);
            _logService.LogInfo($"Tổng số scene: {totalScenes}", jId, tag);
            _logService.LogInfo($"Điểm chuyển cảnh hợp lệ: {validCutPoints}", jId, tag);
            _logService.LogInfo($"Người dùng yêu cầu: {reqTransCount} transition ({reqTransType})", jId, tag);

            if (reqTransCount > validCutPoints && validCutPoints > 0)
            {
                _logService.LogWarning($"Video chỉ có {validCutPoints} điểm chuyển cảnh hợp lệ.", jId, tag);
                _logService.LogWarning($"Số lượng yêu cầu: {reqTransCount}.", jId, tag);
                _logService.LogInfo($"Tự động giảm xuống: {job.TimelinePlan.ActiveTransitionsCount}.", jId, tag);
            }

            if (job.TimelinePlan.ActiveTransitionsCount > 0)
            {
                _logService.LogInfo("Đang phân bố transition...", jId, tag);
                int tIndex = 1;
                foreach (var trans in job.TimelinePlan.Transitions.Where(t => t.IsActiveTransition))
                {
                    _logService.LogInfo($"Transition #{tIndex++}: Scene {trans.FromSceneIndex:D2} → Scene {trans.ToSceneIndex:D2} | Type: {trans.TransitionType} | Duration: {trans.DurationSeconds:F2}s", jId, tag);
                }
            }
            _logService.LogInfo($"Tổng transition thực tế: {job.TimelinePlan.ActiveTransitionsCount}", jId, tag);

            if (job.TimelinePlan.RequiresVideoTrimming)
            {
                _logService.LogInfo($"Video dài hơn voice. Đang cắt video xuống đúng {job.TimelinePlan.TargetMasterDurationSeconds:F2}s", jId, tag);
            }
            else if (job.TimelinePlan.RequiresVideoLooping)
            {
                _logService.LogInfo($"Video ngắn hơn voice ({job.VideoMetadatas[0].DurationSeconds:F2}s < {job.TimelinePlan.TargetMasterDurationSeconds:F2}s). Tự động lặp lại video ({job.TimelinePlan.TotalVideoLoops} vòng lặp)", jId, tag);
            }

            var cropModeDesc = job.Preset.CropMode switch
            {
                CropMode.FitWithBlur => "Nền mờ tự động (Fit with Blur)",
                CropMode.CenterCrop => "Cắt giữa (Center Crop)",
                CropMode.FitBlackBars => "Vừa khung viền đen",
                _ => "Kéo giãn"
            };
            _logService.LogInfo($"Căn khung hình: {job.Preset.ResolutionWidth}x{job.Preset.ResolutionHeight} ({cropModeDesc})", jId, tag);

            // 5. Determine output file path
            var outputDir = _settingsService.CurrentSettings.OutputDirectory;
            Directory.CreateDirectory(outputDir);

            string formattedName;
            if (!string.IsNullOrWhiteSpace(job.CustomOutputName))
            {
                var rawName = job.CustomOutputName.Trim();
                var invalidChars = Path.GetInvalidFileNameChars();
                formattedName = new string(rawName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            }
            else
            {
                var baseOutputName = Path.GetFileNameWithoutExtension(job.VideoPaths[0]);
                var voiceName = Path.GetFileNameWithoutExtension(job.VoicePath);
                var pattern = _settingsService.CurrentSettings.OutputNamingPattern ?? "{original_name}_edited";

                formattedName = pattern
                    .Replace("{original_name}", baseOutputName)
                    .Replace("{voice_name}", voiceName)
                    .Replace("{index}", job.OrderIndex.ToString("D3"))
                    .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"));
            }

            var finalOutputPath = Path.Combine(outputDir, $"{formattedName}.mp4");

            // Apply Overwrite Policy
            finalOutputPath = ResolveOutputFilePath(finalOutputPath, _settingsService.CurrentSettings.OverwritePolicy);
            job.OutputPath = finalOutputPath;

            // 6. Render Video
            UpdateJobState(job, JobStatus.Rendering, "Đang mã hóa video...", 30);
            _logService.LogInfo($"Bắt đầu xuất video: {Path.GetFileName(finalOutputPath)}", jId, tag);
            _logService.LogInfo($"Độ phân giải: {job.Preset.ResolutionWidth}x{job.Preset.ResolutionHeight} | FPS: {job.Preset.Fps} | Audio: AAC {job.Preset.AudioBitrateKbps}k", jId, tag);

            int lastMilestone = 0;
            await _videoRenderer.RenderAsync(
                job,
                job.TimelinePlan,
                report =>
                {
                    job.ProgressPercentage = Math.Clamp(report.ProgressPercentage, 0, 100);
                    job.CurrentStepDescription = report.StepDescription;
                    job.CurrentFps = report.CurrentFps;
                    job.Speed = report.Speed;
                    job.EstimatedTimeRemaining = report.EstimatedTimeRemaining;
                    JobProgressUpdated?.Invoke(job, report);

                    int currentMilestone = (int)(report.ProgressPercentage / 25) * 25;
                    if (currentMilestone > lastMilestone && currentMilestone <= 100)
                    {
                        lastMilestone = currentMilestone;
                        _logService.LogInfo($"Tiến độ FFmpeg: {currentMilestone}% (Tốc độ: {report.Speed:F1}x | FPS: {report.CurrentFps:F0})", jId, tag);
                    }
                },
                ct).ConfigureAwait(false);

            // 7. Complete
            job.Status = JobStatus.Completed;
            job.ProgressPercentage = 100;
            job.CurrentStepDescription = "Hoàn thành";
            job.CompletedAt = DateTime.Now;
            _tempFileManager.CleanupJobTempDirectory(job.Id);

            var outSizeMb = File.Exists(job.OutputPath) ? new FileInfo(job.OutputPath).Length / (1024.0 * 1024.0) : 0;
            var elapsedSec = (job.CompletedAt.Value - job.StartedAt.Value).TotalSeconds;

            _logService.LogSuccess($"✓ Hoàn thành: {Path.GetFileName(job.OutputPath)} ({outSizeMb:F2} MB trong {elapsedSec:F1}s)", jId, tag);
            JobStatusChanged?.Invoke(job);
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Canceled;
            job.CurrentStepDescription = "Đã hủy";
            _tempFileManager.CleanupJobTempDirectory(job.Id);
            _logService.LogWarning($"Công việc đã bị hủy bởi người dùng.", jId, tag);
            JobStatusChanged?.Invoke(job);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CurrentStepDescription = "Thất bại";
            _tempFileManager.CleanupJobTempDirectory(job.Id);
            _logService.LogError($"[LỖI] Xuất video thất bại: {ex.Message}", ex, jId, tag);
            JobStatusChanged?.Invoke(job);
        }
    }

    private void UpdateJobState(VideoJob job, JobStatus status, string stepDesc, double progress)
    {
        lock (_lock)
        {
            job.Status = status;
            job.CurrentStepDescription = stepDesc;
            job.ProgressPercentage = progress;
        }
        JobStatusChanged?.Invoke(job);
    }

    private static string ResolveOutputFilePath(string targetPath, OverwritePolicy policy)
    {
        if (!File.Exists(targetPath) || policy == OverwritePolicy.Overwrite)
            return targetPath;

        if (policy == OverwritePolicy.Skip)
            throw new InvalidOperationException($"File output đã tồn tại và chính sách là Bỏ qua: {targetPath}");

        // AutoRename
        var dir = Path.GetDirectoryName(targetPath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);

        int counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{fileName}_{counter:D3}{ext}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }
}
