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
            _logService.LogInfo($"[CHẾ ĐỘ] OneShot Video TikTok", jId, tag);
            _logService.LogInfo($"Video gốc: {job.VideoFileName}", jId, tag);
            _logService.LogInfo($"Audio/Voice: {job.VoiceFileName}", jId, tag);

            // 1. Validate files
            var videoFile = !string.IsNullOrEmpty(job.VideoPath) ? job.VideoPath : (job.VideoPaths.Count > 0 ? job.VideoPaths[0] : "");
            if (string.IsNullOrEmpty(videoFile) || !File.Exists(videoFile))
            {
                throw new FileNotFoundException($"File video đầu vào không tồn tại: {videoFile}");
            }
            if (string.IsNullOrEmpty(job.VoicePath) || !File.Exists(job.VoicePath))
            {
                throw new FileNotFoundException($"File âm thanh/giọng đọc không tồn tại: {job.VoicePath}");
            }

            // 2. Video Analysis (FFprobe)
            UpdateJobState(job, JobStatus.AnalyzingVideo, "Đang phân tích video...", 5);
            _logService.LogInfo("Đang kiểm tra thông tin video OneShot qua FFprobe...", jId, tag);
            job.VideoMetadatas.Clear();
            var vInfo = await _probeService.ProbeFileAsync(videoFile, ct).ConfigureAwait(false);
            job.VideoMetadatas.Add(vInfo);
            _logService.LogInfo($"Video: {vInfo.Width}x{vInfo.Height}, {vInfo.Fps:F0} FPS, Thời lượng: {vInfo.DurationSeconds:F2}s, Codec: {vInfo.VideoCodec}", jId, tag);

            // 3. Voice Analysis (Silence detection & Master Timeline)
            UpdateJobState(job, JobStatus.AnalyzingVoice, "Đang phân tích âm thanh...", 15);

            if (!job.EnableSilenceRemoval)
            {
                var aInfo = await _probeService.ProbeFileAsync(job.VoicePath, ct).ConfigureAwait(false);
                var rawDur = Math.Max(0.1, aInfo.DurationSeconds);
                job.VoiceAnalysis = new AudioAnalysisResult
                {
                    OriginalDurationSeconds = rawDur,
                    ProcessedDurationSeconds = rawDur,
                    SpeechSegments = new List<SpeechSegment>
                    {
                        new SpeechSegment(1, 0, rawDur)
                    }
                };
                _logService.LogInfo("Cắt khoảng lặng: TẮT (Giữ nguyên toàn bộ âm thanh gốc).", jId, tag);
            }
            else
            {
                _logService.LogInfo("Đang phân tích audio/voice & cắt khoảng lặng...", jId, tag);
                job.VoiceAnalysis = await _audioAnalyzer.AnalyzeVoiceAsync(
                    job.VoicePath,
                    job.Preset.SilenceThresholdDb,
                    job.Preset.MinSilenceDurationMs,
                    job.Preset.PaddingBeforeMs,
                    job.Preset.PaddingAfterMs,
                    400,
                    ct).ConfigureAwait(false);
            }

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
                _logService.LogInfo($"Cắt âm thanh: -{vTrimStart:F1}s đầu, -{vTrimEnd:F1}s cuối.", jId, tag);
            }

            _logService.LogInfo($"Thời lượng audio gốc: {job.VoiceAnalysis.OriginalDurationSeconds:F2}s", jId, tag);
            _logService.LogInfo($"Cắt khoảng lặng: Đã cắt {job.VoiceAnalysis.SilenceDurationRemovedSeconds:F2}s ({job.VoiceAnalysis.SilenceRemovalPercentage:F1}%)", jId, tag);
            _logService.LogSuccess($"Thời lượng Master Timeline: {job.VoiceAnalysis.ProcessedDurationSeconds:F2}s", jId, tag);

            if (extraEnd > 0)
            {
                _logService.LogInfo($"Dư cuối video: +{extraEnd:F1}s | Tổng thời lượng: {(job.VoiceAnalysis.ProcessedDurationSeconds + extraEnd):F2}s", jId, tag);
            }

            // 4. OneShot Timeline & Smart Jump Cut Planning
            UpdateJobState(job, JobStatus.BuildingTimeline, "Đang xây dựng timeline OneShot...", 25);
            _logService.LogInfo("Đang xây dựng timeline OneShot (Smart Cut & Jump Cut)...", jId, tag);

            // Dò các điểm chuyển động / thay đổi khung hình để chọn điểm cắt
            if (job.Preset.EnableSmartSceneCut && _sceneDetector != null)
            {
                try
                {
                    job.DetectedScenes = await _sceneDetector.DetectScenesAsync(videoFile, 0.3, ct).ConfigureAwait(false);
                    if (job.DetectedScenes.Count > 1)
                    {
                        _logService.LogInfo($"Phát hiện {job.DetectedScenes.Count} điểm cắt chuyển động tự nhiên.", jId, tag);
                    }
                    else
                    {
                        _logService.LogInfo("Video OneShot liền mạch. Tự động chia nhịp Smart Cut thẩm mỹ.", jId, tag);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Scene detection failed, using rhythmic smart cut.");
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
            job.OneShotClips = job.TimelinePlan.OneShotClips;

            _logService.LogInfo($"Số đoạn nhịp (Clips) sử dụng: {job.TimelinePlan.OneShotClips.Count}", jId, tag);
            _logService.LogInfo($"Transition yêu cầu: {reqTransCount} ({reqTransType}) | Transition thực tế: {job.TimelinePlan.ActiveTransitionsCount}", jId, tag);

            if (job.TimelinePlan.ActiveTransitionsCount > 0)
            {
                int tIndex = 1;
                foreach (var trans in job.TimelinePlan.Transitions.Where(t => t.IsActiveTransition))
                {
                    _logService.LogInfo($"Transition #{tIndex++}: Đoạn {trans.FromSceneIndex:D2} → Đoạn {trans.ToSceneIndex:D2} | Kiểu: {trans.TransitionType} ({trans.DurationSeconds:F2}s)", jId, tag);
                }
            }

            if (job.TimelinePlan.RequiresVideoLooping)
            {
                _logService.LogInfo($"Video ngắn hơn audio. Tự động lặp lại video ({job.TimelinePlan.TotalVideoLoops} vòng lặp)", jId, tag);
            }
            else
            {
                _logService.LogInfo($"Video dài hơn audio. Smart Cut đã chọn lọc {job.TimelinePlan.OneShotClips.Count} đoạn trải đều toàn video.", jId, tag);
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
