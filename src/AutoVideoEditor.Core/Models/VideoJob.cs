using System.ComponentModel;
using System.Runtime.CompilerServices;
using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

public class VideoJob : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private Guid _id = Guid.NewGuid();
    public Guid Id { get => _id; set => SetProperty(ref _id, value); }

    private int _orderIndex;
    public int OrderIndex { get => _orderIndex; set => SetProperty(ref _orderIndex, value); }

    private List<string> _videoPaths = new();
    public List<string> VideoPaths 
    { 
        get => _videoPaths; 
        set 
        { 
            SetProperty(ref _videoPaths, value); 
            OnPropertyChanged(nameof(VideoFileName)); 
        } 
    }

    private string _voicePath = string.Empty;
    public string VoicePath 
    { 
        get => _voicePath; 
        set 
        { 
            SetProperty(ref _voicePath, value); 
            OnPropertyChanged(nameof(VoiceFileName)); 
        } 
    }

    private string _outputPath = string.Empty;
    public string OutputPath 
    { 
        get => _outputPath; 
        set 
        { 
            SetProperty(ref _outputPath, value); 
            OnPropertyChanged(nameof(OutputFileName)); 
        } 
    }

    private ExportPreset _preset = new();
    public ExportPreset Preset { get => _preset; set => SetProperty(ref _preset, value); }

    // Per-job custom trim & outro padding overrides
    private double _videoTrimStartSeconds;
    public double VideoTrimStartSeconds { get => _videoTrimStartSeconds; set => SetProperty(ref _videoTrimStartSeconds, value); }

    private double _videoTrimEndSeconds;
    public double VideoTrimEndSeconds { get => _videoTrimEndSeconds; set => SetProperty(ref _videoTrimEndSeconds, value); }

    private double _voiceTrimStartSeconds;
    public double VoiceTrimStartSeconds { get => _voiceTrimStartSeconds; set => SetProperty(ref _voiceTrimStartSeconds, value); }

    private double _voiceTrimEndSeconds;
    public double VoiceTrimEndSeconds { get => _voiceTrimEndSeconds; set => SetProperty(ref _voiceTrimEndSeconds, value); }

    private double _extraEndPaddingSeconds;
    public double ExtraEndPaddingSeconds { get => _extraEndPaddingSeconds; set => SetProperty(ref _extraEndPaddingSeconds, value); }

    private string? _customOutputName;
    public string? CustomOutputName 
    { 
        get => _customOutputName; 
        set 
        { 
            SetProperty(ref _customOutputName, value); 
            OnPropertyChanged(nameof(OutputFileName)); 
        } 
    }

    private int? _customTransitionCount;
    public int? CustomTransitionCount { get => _customTransitionCount; set => SetProperty(ref _customTransitionCount, value); }

    private TransitionType? _customTransitionType;
    public TransitionType? CustomTransitionType { get => _customTransitionType; set => SetProperty(ref _customTransitionType, value); }

    public List<SceneSegment> DetectedScenes { get; set; } = new();
    public List<TransitionPlanItem> PlannedTransitions { get; set; } = new();

    // Primary Display Names
    public string VideoFileName => VideoPaths.Count == 1 
        ? Path.GetFileName(VideoPaths[0]) 
        : VideoPaths.Count > 1 
            ? $"{Path.GetFileName(VideoPaths[0])} (+{VideoPaths.Count - 1} video khác)" 
            : "(Chưa chọn video)";

    public string VoiceFileName => !string.IsNullOrEmpty(VoicePath) 
        ? Path.GetFileName(VoicePath) 
        : "(Chưa chọn giọng đọc)";

    public string OutputFileName => !string.IsNullOrEmpty(OutputPath) 
        ? Path.GetFileName(OutputPath) 
        : !string.IsNullOrWhiteSpace(CustomOutputName)
            ? $"{CustomOutputName}.mp4"
            : "(Chưa tạo)";

    // Execution state
    private JobStatus _status = JobStatus.Pending;
    public JobStatus Status { get => _status; set => SetProperty(ref _status, value); }

    private double _progressPercentage;
    public double ProgressPercentage { get => _progressPercentage; set => SetProperty(ref _progressPercentage, value); }

    private string _currentStepDescription = "Đang chờ";
    public string CurrentStepDescription { get => _currentStepDescription; set => SetProperty(ref _currentStepDescription, value); }

    private double _currentFps;
    public double CurrentFps { get => _currentFps; set => SetProperty(ref _currentFps, value); }

    private double _speed;
    public double Speed { get => _speed; set => SetProperty(ref _speed, value); }

    private TimeSpan? _estimatedTimeRemaining;
    public TimeSpan? EstimatedTimeRemaining { get => _estimatedTimeRemaining; set => SetProperty(ref _estimatedTimeRemaining, value); }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    private string? _encoderUsed;
    public string? EncoderUsed { get => _encoderUsed; set => SetProperty(ref _encoderUsed, value); }

    // Time Tracking
    private DateTime? _createdAt = DateTime.Now;
    public DateTime? CreatedAt { get => _createdAt; set => SetProperty(ref _createdAt, value); }

    private DateTime? _startedAt;
    public DateTime? StartedAt { get => _startedAt; set => SetProperty(ref _startedAt, value); }

    private DateTime? _completedAt;
    public DateTime? CompletedAt { get => _completedAt; set => SetProperty(ref _completedAt, value); }

    public TimeSpan? ElapsedTime => StartedAt.HasValue 
        ? ((CompletedAt ?? DateTime.Now) - StartedAt.Value) 
        : null;

    // Analysis Results cache
    public AudioAnalysisResult? VoiceAnalysis { get; set; }
    public List<MediaFileInfo> VideoMetadatas { get; set; } = new();
    public TimelinePlan? TimelinePlan { get; set; }

    public List<string> InternalLogs { get; set; } = new();

    public void AddLog(string message)
    {
        InternalLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
