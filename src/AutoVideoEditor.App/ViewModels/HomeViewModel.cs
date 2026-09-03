using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AutoVideoEditor.App.ViewModels;

public partial class MatchedPairItem : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private List<string> _videoPaths = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceFileName))]
    [NotifyPropertyChangedFor(nameof(HasVoice))]
    private string _voicePath = string.Empty;

    public string VideoFileName => VideoPaths.Count == 1 
        ? Path.GetFileName(VideoPaths[0]) 
        : VideoPaths.Count > 1 
            ? $"{Path.GetFileName(VideoPaths[0])} (+{VideoPaths.Count - 1})" 
            : "(Chưa chọn video)";

    public string VoiceFileName => !string.IsNullOrEmpty(VoicePath) 
        ? Path.GetFileName(VoicePath) 
        : "(Chưa gán giọng đọc)";

    public bool HasVoice => !string.IsNullOrEmpty(VoicePath) && File.Exists(VoicePath);

    [ObservableProperty]
    private double _videoTrimStart;

    [ObservableProperty]
    private double _videoTrimEnd;

    [ObservableProperty]
    private double _voiceTrimStart;

    [ObservableProperty]
    private double _voiceTrimEnd;

    [ObservableProperty]
    private double _extraEndPadding;
}

public partial class HomeViewModel : ObservableObject
{
    private readonly IPresetService _presetService;
    private readonly ISettingsService _settingsService;
    private readonly IJobManager _jobManager;
    private readonly ILogService _logService;

    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".mkv", ".avi", ".webm" };
    private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".m4a", ".aac", ".flac" };

    public event Action? NavigateToQueueRequested;

    [ObservableProperty]
    private ObservableCollection<string> _importedVideoPaths = new();

    [ObservableProperty]
    private ObservableCollection<string> _importedVoicePaths = new();

    [ObservableProperty]
    private ObservableCollection<ExportPreset> _availablePresets = new();

    [ObservableProperty]
    private ExportPreset? _selectedPreset;

    [ObservableProperty]
    private ObservableCollection<MatchedPairItem> _matchedPairs = new();

    [ObservableProperty]
    private MatchedPairItem? _selectedPair;

    [ObservableProperty]
    private FileMappingMode _mappingMode = FileMappingMode.ByName;

    [ObservableProperty]
    private string _matchSummaryText = "Chưa có media được thêm vào";

    [ObservableProperty]
    private bool _hasMatchedJobs;

    [ObservableProperty]
    private bool _hasValidJobs;

    [ObservableProperty]
    private int _matchedPairsCount;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    public HomeViewModel(
        IPresetService presetService,
        ISettingsService settingsService,
        IJobManager jobManager,
        ILogService logService)
    {
        _presetService = presetService;
        _settingsService = settingsService;
        _jobManager = jobManager;
        _logService = logService;

        _ = LoadInitialDataAsync();
    }

    private async Task LoadInitialDataAsync()
    {
        var presets = await _presetService.GetAllPresetsAsync();
        AvailablePresets = new ObservableCollection<ExportPreset>(presets);
        SelectedPreset = AvailablePresets.FirstOrDefault();

        var settings = await _settingsService.LoadSettingsAsync();
        OutputDirectory = settings.OutputDirectory;
    }

    partial void OnSelectedPresetChanged(ExportPreset? value)
    {
        if (value == null) return;
        foreach (var pair in MatchedPairs)
        {
            if (pair.VideoTrimStart == 0) pair.VideoTrimStart = value.VideoTrimStartSeconds;
            if (pair.VideoTrimEnd == 0) pair.VideoTrimEnd = value.VideoTrimEndSeconds;
            if (pair.VoiceTrimStart == 0) pair.VoiceTrimStart = value.VoiceTrimStartSeconds;
            if (pair.VoiceTrimEnd == 0) pair.VoiceTrimEnd = value.VoiceTrimEndSeconds;
            if (pair.ExtraEndPadding == 0) pair.ExtraEndPadding = value.ExtraEndPaddingSeconds;
        }
    }

    partial void OnMappingModeChanged(FileMappingMode value)
    {
        UpdateMatchingSummary();
    }

    [RelayCommand]
    public void SelectVideoFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file video",
            Multiselect = true,
            Filter = "Video Files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddVideoFiles(dialog.FileNames);
        }
    }

    [RelayCommand]
    public void SelectVoiceFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file giọng đọc",
            Multiselect = true,
            Filter = "Audio Files (*.mp3;*.wav;*.m4a;*.aac;*.flac)|*.mp3;*.wav;*.m4a;*.aac;*.flac|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddVoiceFiles(dialog.FileNames);
        }
    }

    [RelayCommand]
    public void SelectVideoFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục chứa Video"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            AddVideoFolder(dialog.FolderName);
        }
    }

    [RelayCommand]
    public void SelectVoiceFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục chứa Giọng đọc"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            AddVoiceFolder(dialog.FolderName);
        }
    }

    [RelayCommand]
    public void ChooseOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục xuất video"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            OutputDirectory = dialog.FolderName;
            _settingsService.CurrentSettings.OutputDirectory = dialog.FolderName;
            _ = _settingsService.SaveSettingsAsync(_settingsService.CurrentSettings);
        }
    }

    [RelayCommand]
    public void ClearAllMedia()
    {
        ImportedVideoPaths.Clear();
        ImportedVoicePaths.Clear();
        MatchedPairs.Clear();
        UpdateMatchingSummary();
    }

    [RelayCommand]
    public void RemovePair(MatchedPairItem? item)
    {
        if (item == null) return;
        MatchedPairs.Remove(item);
        foreach (var v in item.VideoPaths)
        {
            ImportedVideoPaths.Remove(v);
        }
        if (!string.IsNullOrEmpty(item.VoicePath))
        {
            ImportedVoicePaths.Remove(item.VoicePath);
        }
        ReindexPairs();
        UpdateSummaryCounters();
    }

    [RelayCommand]
    public void ChangeVoiceForPair(MatchedPairItem? item)
    {
        if (item == null) return;
        var dialog = new OpenFileDialog
        {
            Title = $"Chọn giọng đọc cho: {item.VideoFileName}",
            Multiselect = false,
            Filter = "Audio Files (*.mp3;*.wav;*.m4a;*.aac;*.flac)|*.mp3;*.wav;*.m4a;*.aac;*.flac|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true && File.Exists(dialog.FileName))
        {
            item.VoicePath = dialog.FileName;
            if (!ImportedVoicePaths.Contains(dialog.FileName))
            {
                ImportedVoicePaths.Add(dialog.FileName);
            }
            UpdateSummaryCounters();
        }
    }

    [RelayCommand]
    public void ChangeVideoForPair(MatchedPairItem? item)
    {
        if (item == null) return;
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file video thay thế",
            Multiselect = false,
            Filter = "Video Files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true && File.Exists(dialog.FileName))
        {
            item.VideoPaths = new List<string> { dialog.FileName };
            if (!ImportedVideoPaths.Contains(dialog.FileName))
            {
                ImportedVideoPaths.Add(dialog.FileName);
            }
            OnPropertyChanged(nameof(MatchedPairs));
            UpdateSummaryCounters();
        }
    }

    [RelayCommand]
    public void AddCustomRow()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file video để thêm dòng mới",
            Multiselect = false,
            Filter = "Video Files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true && File.Exists(dialog.FileName))
        {
            AddVideoFiles(new[] { dialog.FileName });
        }
    }

    [RelayCommand]
    public async Task AutoCreateVideosAsync()
    {
        if (SelectedPreset == null)
        {
            MessageBox.Show("Vui lòng chọn một mẫu xuất (Preset) trước khi tạo video.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var validPairs = MatchedPairs.Where(p => p.HasVoice).ToList();
        if (validPairs.Count == 0)
        {
            MessageBox.Show("Chưa có video nào được gán giọng đọc hợp lệ. Vui lòng thêm giọng đọc trước khi tạo video.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var jobs = new List<VideoJob>();
        int idx = 1;
        foreach (var pair in validPairs)
        {
            var job = new VideoJob
            {
                OrderIndex = idx++,
                VideoPaths = pair.VideoPaths.ToList(),
                VoicePath = pair.VoicePath,
                Preset = SelectedPreset.Clone(),
                VideoTrimStartSeconds = pair.VideoTrimStart,
                VideoTrimEndSeconds = pair.VideoTrimEnd,
                VoiceTrimStartSeconds = pair.VoiceTrimStart,
                VoiceTrimEndSeconds = pair.VoiceTrimEnd,
                ExtraEndPaddingSeconds = pair.ExtraEndPadding
            };
            jobs.Add(job);
        }

        _jobManager.AddJobs(jobs);

        // Switch to Queue View and start queue
        NavigateToQueueRequested?.Invoke();
        _ = Task.Run(() => _jobManager.StartQueueAsync());
    }

    public void AddVideoFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (File.Exists(file) && VideoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()) && !ImportedVideoPaths.Contains(file))
            {
                ImportedVideoPaths.Add(file);
            }
        }
        UpdateMatchingSummary();
    }

    public void AddVoiceFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (File.Exists(file) && AudioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()) && !ImportedVoicePaths.Contains(file))
            {
                ImportedVoicePaths.Add(file);
            }
        }
        UpdateMatchingSummary();
    }

    public void AddVideoFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        var foundFiles = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        AddVideoFiles(foundFiles);
    }

    public void AddVoiceFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        var foundFiles = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        AddVoiceFiles(foundFiles);
    }

    public void HandleVideoDrop(string[] droppedPaths)
    {
        foreach (var p in droppedPaths)
        {
            if (Directory.Exists(p)) AddVideoFolder(p);
            else if (File.Exists(p)) AddVideoFiles(new[] { p });
        }
    }

    public void HandleVoiceDrop(string[] droppedPaths)
    {
        foreach (var p in droppedPaths)
        {
            if (Directory.Exists(p)) AddVoiceFolder(p);
            else if (File.Exists(p)) AddVoiceFiles(new[] { p });
        }
    }

    private void UpdateMatchingSummary()
    {
        var defaultVTrimStart = SelectedPreset?.VideoTrimStartSeconds ?? 0.0;
        var defaultVTrimEnd = SelectedPreset?.VideoTrimEndSeconds ?? 0.0;
        var defaultVoiceTrimStart = SelectedPreset?.VoiceTrimStartSeconds ?? 0.0;
        var defaultVoiceTrimEnd = SelectedPreset?.VoiceTrimEndSeconds ?? 0.0;
        var defaultExtraEnd = SelectedPreset?.ExtraEndPaddingSeconds ?? 0.0;

        var pairs = new List<MatchedPairItem>();
        var usedVoices = new HashSet<string>();

        int idx = 1;
        if (MappingMode == FileMappingMode.ByName)
        {
            foreach (var video in ImportedVideoPaths)
            {
                var vName = Path.GetFileNameWithoutExtension(video);
                var vClean = CleanName(vName);

                var matchedVoice = ImportedVoicePaths
                    .Where(v => !usedVoices.Contains(v))
                    .OrderByDescending(v => CalculateSimilarity(vClean, CleanName(Path.GetFileNameWithoutExtension(v))))
                    .FirstOrDefault();

                var voiceToAssign = "";
                if (matchedVoice != null && CalculateSimilarity(vClean, CleanName(Path.GetFileNameWithoutExtension(matchedVoice))) > 0.2)
                {
                    voiceToAssign = matchedVoice;
                    usedVoices.Add(matchedVoice);
                }

                pairs.Add(new MatchedPairItem
                {
                    Index = idx++,
                    VideoPaths = new List<string> { video },
                    VoicePath = voiceToAssign,
                    VideoTrimStart = defaultVTrimStart,
                    VideoTrimEnd = defaultVTrimEnd,
                    VoiceTrimStart = defaultVoiceTrimStart,
                    VoiceTrimEnd = defaultVoiceTrimEnd,
                    ExtraEndPadding = defaultExtraEnd
                });
            }
        }
        else // ByOrder
        {
            for (int i = 0; i < ImportedVideoPaths.Count; i++)
            {
                var vPath = ImportedVideoPaths[i];
                var voiceToAssign = i < ImportedVoicePaths.Count ? ImportedVoicePaths[i] : "";

                pairs.Add(new MatchedPairItem
                {
                    Index = idx++,
                    VideoPaths = new List<string> { vPath },
                    VoicePath = voiceToAssign,
                    VideoTrimStart = defaultVTrimStart,
                    VideoTrimEnd = defaultVTrimEnd,
                    VoiceTrimStart = defaultVoiceTrimStart,
                    VoiceTrimEnd = defaultVoiceTrimEnd,
                    ExtraEndPadding = defaultExtraEnd
                });
            }
        }

        MatchedPairs = new ObservableCollection<MatchedPairItem>(pairs);
        UpdateSummaryCounters();
    }

    private void ReindexPairs()
    {
        for (int i = 0; i < MatchedPairs.Count; i++)
        {
            MatchedPairs[i].Index = i + 1;
        }
    }

    private void UpdateSummaryCounters()
    {
        var validVoicePairs = MatchedPairs.Count(p => p.HasVoice);
        MatchedPairsCount = validVoicePairs;
        HasMatchedJobs = MatchedPairs.Count > 0;
        HasValidJobs = validVoicePairs > 0;

        if (ImportedVideoPaths.Count == 0 && MatchedPairs.Count == 0)
        {
            MatchSummaryText = "Kéo thả hoặc chọn Video để bắt đầu";
        }
        else if (validVoicePairs == 0)
        {
            MatchSummaryText = $"Đã tải {MatchedPairs.Count} video. (Chưa có giọng đọc - kéo thả file voice hoặc bấm '🎙️ Chọn Voice' ở từng dòng)";
        }
        else if (validVoicePairs == MatchedPairs.Count)
        {
            MatchSummaryText = $"✓ Sẵn sàng: Đã ghép đủ {validVoicePairs}/{MatchedPairs.Count} video có giọng đọc.";
        }
        else
        {
            var missing = MatchedPairs.Count - validVoicePairs;
            MatchSummaryText = $"✓ Sẵn sàng tạo {validVoicePairs} video có giọng đọc ({missing} video chưa có giọng đọc sẽ được bỏ qua).";
        }
    }

    private static string CleanName(string name)
    {
        return new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static double CalculateSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Contains(s2) || s2.Contains(s1)) return 0.8;
        return 0.0;
    }
}
