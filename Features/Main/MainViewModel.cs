using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Infrastructure.Helpers;
using PromptMasterv6.Features.Main.Messages;
using PromptMasterv6.Features.Launcher.Messages;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Sidebar;
using PromptMasterv6.Features.Workspace;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace PromptMasterv6.Features.Main;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDataService _dataService;
    private readonly IDataService _localDataService;
    private readonly IGlobalKeyService _keyService;
    private readonly IAiService _aiService;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboardService;
    private readonly IWindowManager _windowManager;
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IVariableService _variableService;
    private readonly IContentConverterService _contentConverterService;
    private readonly IWebTargetService _webTargetService;

    private DispatcherTimer _timer;
    private readonly Subject<System.Reactive.Unit> _saveSubject = new();
    private readonly Subject<System.Reactive.Unit> _saveLocalSettingsSubject = new();

    private EventHandler? _onLauncherTriggeredHandler;
    private PropertyChangedEventHandler? _localConfigPropertyChangedHandler;

    private bool _isSimulatingKeys;
    public void SetSimulatingKeys(bool value) => _isSimulatingKeys = value;

    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private LocalSettings localConfig;
    [ObservableProperty] private bool isFullMode = true;

    public ISettingsService SettingsService => _settingsService;

    [ObservableProperty] private string syncTimeDisplay = "Now";
    [ObservableProperty] private ICollectionView? filesView;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;

    [ObservableProperty] private ObservableCollection<PromptItem> files = new();

    [ObservableProperty] private PromptItem? selectedFile;

    [ObservableProperty] private SidebarViewModel? sidebarVM;
    [ObservableProperty] private WorkspaceViewModel? workspaceVM;

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        IsEditMode = false;
        PreviewContent = _contentConverterService.ConvertHtmlToMarkdown(value?.Content);
        SafeParseVariables(value?.Content ?? "");
    }

    [RelayCommand]
    private void RenameFile(PromptItem? item)
    {
        if (item != null)
        {
            item.IsRenaming = true;
        }
    }

    private void SafeParseVariables(string content)
    {
        try
        {
            _variableService.ParseVariables(content, Variables);
            HasVariables = _variableService.HasVariables(Variables);
        }
        catch (Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "变量解析失败", "SafeParseVariables");
        }
    }

    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";

    [ObservableProperty] private bool isDirty;

    private string? _originalContentBeforeEdit;

    public MarkdownPipeline Pipeline { get; }

    [ObservableProperty] private string? previewContent;


    public MainViewModel(
        ISettingsService settingsService,
        IAiService aiService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService,
        IGlobalKeyService keyService,
        IDialogService dialogService,
        IClipboardService clipboardService,
        IWindowManager windowManager,
        IHotkeyService hotkeyService,
        IVariableService variableService,
        IContentConverterService contentConverterService,
        IWebTargetService webTargetService)
    {
        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        _aiService = aiService;
        _dataService = dataService;
        _localDataService = localDataService;
        _keyService = keyService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _settingsService = settingsService;
        _windowManager = windowManager;
        _hotkeyService = hotkeyService;
        _variableService = variableService;
        _contentConverterService = contentConverterService;
        _webTargetService = webTargetService;

        Config = settingsService.Config;
        LocalConfig = settingsService.LocalConfig;

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, (_, __) =>
        {
            UpdateFilesViewFilter();
            FilesView?.Refresh();
            
            if (FilesView != null && !FilesView.IsEmpty)
            {
                var firstItem = FilesView.Cast<PromptItem>().FirstOrDefault();
                SelectedFile = firstItem;
            }
            else
            {
                SelectedFile = null;
            }
        });
        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            SelectedFile = m.File;
            if (m.EnterEditMode) IsEditMode = true;
        });
        WeakReferenceMessenger.Default.Register<RequestMoveFileToFolderMessage>(this, (_, m) => MoveFileToFolder(m.File, m.TargetFolder));
        WeakReferenceMessenger.Default.Register<RequestSaveMessage>(this, (_, __) => RequestSave());
        WeakReferenceMessenger.Default.Register<RequestBackupMessage>(this, async (_, __) => await PerformLocalBackup());
        WeakReferenceMessenger.Default.Register<ToggleWindowMessage>(this, (_, __) => ToggleMainWindow());
        WeakReferenceMessenger.Default.Register<TriggerLauncherMessage>(this, (_, __) => HandleLauncherTriggered());
        WeakReferenceMessenger.Default.Register<JumpToEditPromptMessage>(this, (_, m) =>
        {
            if (m.File != null)
            {
                SelectedFile = m.File;
                IsEditMode = true;
            }
        });
        WeakReferenceMessenger.Default.Register<RequestPromptFileMessage>(this, (r, m) =>
        {
            var file = Files.FirstOrDefault(f => f.Id == m.PromptId);
            m.Reply(new PromptFileResponseMessage { File = file });
        });


        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, __) => UpdateTimeDisplay();
        _timer.Start();

        _saveSubject
            .Throttle(TimeSpan.FromSeconds(5))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(async _ => await PerformLocalBackup());

        _saveLocalSettingsSubject
            .Throttle(TimeSpan.FromSeconds(2))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(_ => _settingsService.SaveLocalConfig());

        _localConfigPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(LocalSettings.Block1Width) ||
                e.PropertyName == nameof(LocalSettings.Block2Width))
            {
                _saveLocalSettingsSubject.OnNext(System.Reactive.Unit.Default);
            }
        };
        LocalConfig.PropertyChanged += _localConfigPropertyChangedHandler;

        _onLauncherTriggeredHandler = (_, __) => HandleLauncherTriggered();
        _keyService.OnLauncherTriggered += _onLauncherTriggeredHandler;

        _keyService.LauncherHotkeyString = Config.LauncherHotkey;
        try { _keyService.Start(); }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "Failed to start GlobalKeyService", "MainViewModel.ctor");
        }

        UpdateWindowHotkeys();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        AppData data;
        try
        {
            data = await _dataService.LoadAsync();
        }
        catch
        {
            data = new AppData();
        }

        if ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0)
        {
            try
            {
                data = await _localDataService.LoadAsync();
            }
            catch
            {
                data = new AppData();
            }
        }

        Files = new ObservableCollection<PromptItem>(data.Files ?? new());

        Files.CollectionChanged += OnFilesCollectionChanged;
        foreach (var item in Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }

        Folders = new ObservableCollection<FolderItem>(data.Folders ?? new());
        if (Folders.Count == 0)
        {
            var defaultFolder = new FolderItem { Name = "默认" };
            Folders.Add(defaultFolder);
            SelectedFolder = defaultFolder;
        }
        else
        {
            SelectedFolder = Folders.FirstOrDefault();
        }

        if (SelectedFolder != null)
        {
            foreach (var f in Files)
            {
                if (string.IsNullOrWhiteSpace(f.FolderId))
                {
                    f.FolderId = SelectedFolder.Id;
                }
            }
        }

        FilesView = CollectionViewSource.GetDefaultView(Files);
        UpdateFilesViewFilter();
        FilesView?.Refresh();

        if (FilesView != null && !FilesView.IsEmpty)
        {
            var firstItem = FilesView.Cast<PromptItem>().FirstOrDefault();
            if (firstItem != null)
            {
                SelectedFile = firstItem;
            }
        }

        IsDirty = false;
    }

    public void UpdateFilesViewFilter()
    {
        if (FilesView == null) return;

        var selectedFolderId = SelectedFolder?.Id;
        FilesView.Filter = item =>
        {
            if (item is not PromptItem f) return false;
            if (string.IsNullOrWhiteSpace(selectedFolderId)) return true;
            return string.Equals(f.FolderId, selectedFolderId, StringComparison.Ordinal);
        };
    }

    [RelayCommand]
    private void EnterFullMode()
    {
        IsFullMode = true;
    }

    public void OnWindowHotkeyPressed()
    {
        ToggleMainWindow();
    }

    public void SimulateFullWindowHotkey()
    {
        _hotkeyService.SimulateHotkey(Config.FullWindowHotkey);
    }

    public void ToggleModeViaHotkey()
    {
        EnterFullMode();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManager.ShowSettingsWindow();
    }

    [RelayCommand]
    private void ImportMarkdownFiles()
    {
        string filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*";
        var files = _dialogService.ShowOpenFilesDialog(filter);

        if (files == null || files.Length == 0) return;

        var targetFolder = SelectedFolder;
        if (targetFolder == null)
        {
            targetFolder = new FolderItem { Name = "导入" };
            Folders.Add(targetFolder);
            SelectedFolder = targetFolder;
        }

        foreach (var filePath in files)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var title = Path.GetFileNameWithoutExtension(filePath);
                Files.Add(new PromptItem
                {
                    Title = title,
                    Content = content,
                    FolderId = targetFolder.Id,
                    LastModified = DateTime.Now
                });
            }
            catch
            {
            }
        }

        UpdateFilesViewFilter();
        FilesView?.Refresh();
        RequestSave();
    }

    [RelayCommand]
    private void DeleteFile(PromptItem? file)
    {
        if (file == null) return;
        Files.Remove(file);
        if (SelectedFile == file) SelectedFile = null;
        RequestSave();
    }

    [RelayCommand]
    private void ChangeFileIcon(PromptItem? file)
    {
        if (file == null) return;
        var dialog = new PromptMasterv6.IconInputDialog(file.IconGeometry);
        if (dialog.ShowDialog() == true)
        {
            file.IconGeometry = dialog.ResultGeometry;
            RequestSave();
        }
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (SelectedFile == null)
        {
            IsEditMode = false;
            return;
        }

        if (IsEditMode)
        {
            bool contentChanged = !string.Equals(_originalContentBeforeEdit, SelectedFile.Content, StringComparison.Ordinal);

            if (contentChanged)
            {
                SelectedFile.LastModified = DateTime.Now;
                RequestSave();
            }

            IsEditMode = false;
            PreviewContent = _contentConverterService.ConvertHtmlToMarkdown(SelectedFile.Content);
            _originalContentBeforeEdit = null;
            return;
        }

        _originalContentBeforeEdit = SelectedFile.Content;
        IsEditMode = true;
    }

    [RelayCommand]
    private void CopyCompiledText()
    {
        var text = _variableService.CompileContent(SelectedFile?.Content, Variables, AdditionalInput);
        if (string.IsNullOrWhiteSpace(text)) return;
        _clipboardService.SetClipboard(text);
    }

    [RelayCommand]
    private async Task SendDefaultWebTarget()
    {
        if (SelectedFile == null) return;

        if (HasVariables)
        {
            foreach (var v in Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Value))
                {
                    _dialogService.ShowAlert("请先填写所有变量值。", "变量未填");
                    return;
                }
            }
        }

        var content = _variableService.CompileContent(SelectedFile?.Content, Variables, AdditionalInput);
        await _webTargetService.SendToDefaultTargetAsync(content, Config);
        AdditionalInput = "";
    }

    [RelayCommand]
    private async Task OpenWebTarget(WebTarget? target)
    {
        if (target == null || SelectedFile == null) return;

        if (HasVariables)
        {
            foreach (var v in Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Value))
                {
                    _dialogService.ShowAlert("请先填写所有变量值。", "变量未填");
                    return;
                }
            }
        }

        var content = _variableService.CompileContent(SelectedFile?.Content, Variables, AdditionalInput);
        await _webTargetService.OpenWebTargetAsync(target, content);
        AdditionalInput = "";
    }

    [RelayCommand]
    private void SearchOnGitHub()
    {
        var query = AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return;
        }

        try
        {
            var url = $"https://github.com/search?q={System.Uri.EscapeDataString(query)}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            AdditionalInput = "";
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "SearchOnGitHub Failed", "MainViewModel");
            _dialogService.ShowAlert($"打开 GitHub 失败: {ex.Message}", "错误");
        }
    }

    private void ToggleMainWindow()
    {
        var win = Application.Current.MainWindow as MainWindow;
        if (win == null) return;

        win.ToggleWindowVisibility();
    }

    private static string NormalizeSymbols(string s) =>
        StringUtils.NormalizeSymbols(s);

    [SupportedOSPlatform("windows")]
    public void UpdateWindowHotkeys()
    {
        _hotkeyService.RegisterWindowHotkey("ToggleFullWindowHotkey", Config.FullWindowHotkey, () => ToggleWindowToMode(true));
        
        _hotkeyService.RegisterWindowHotkey("ToggleLaunchBarHotkey", Config.LaunchBarHotkey, () => 
        {
            Config.EnableLaunchBar = !Config.EnableLaunchBar;
        });
    }

    private void ToggleWindowToMode(bool targetFull)
    {
        var win = Application.Current.MainWindow;
        if (win == null) return;

        if (win.Visibility != Visibility.Visible)
        {
            IsFullMode = true;
            win.Show();
            win.Activate();
            return;
        }

        win.Hide();
    }

    public void MoveFileToFolder(PromptItem f, FolderItem t)
    {
        if (f == null || t == null || f.FolderId == t.Id) return;
        f.FolderId = t.Id;
        FilesView?.Refresh();
        if (SelectedFile == f) SelectedFile = null;
        RequestSave();
    }

    [RelayCommand]
    private void RequestSave()
    {
        if (!IsDirty) IsDirty = true;
        _saveSubject.OnNext(System.Reactive.Unit.Default);
    }

    public async Task PerformLocalBackup()
    {
        try
        {
            await _localDataService.SaveAsync(Folders, Files);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "Failed to perform local backup", "MainViewModel.PerformLocalBackup");
        }
    }

    private void OnFilesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PromptItem item in e.NewItems)
            {
                item.PropertyChanged += OnFilePropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (PromptItem item in e.OldItems)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        RequestSave();
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptItem.LastModified))
            return;

        if (e.PropertyName == nameof(PromptItem.Content) && sender == SelectedFile)
        {
            _variableService.ParseVariables(SelectedFile?.Content ?? "", Variables);
            HasVariables = _variableService.HasVariables(Variables);
        }

        RequestSave();
    }

    private void UpdateTimeDisplay()
    {
        if (LocalConfig?.LastCloudSyncTime == null)
        {
            SyncTimeDisplay = "-";
            return;
        }

        var diff = DateTime.Now - LocalConfig.LastCloudSyncTime.Value;

        if (diff.TotalSeconds < 60)
        {
            SyncTimeDisplay = $"{(int)diff.TotalSeconds}s";
        }
        else if (diff.TotalMinutes < 60)
        {
            SyncTimeDisplay = $"{(int)diff.TotalMinutes}min";
        }
        else if (diff.TotalHours < 24)
        {
            SyncTimeDisplay = $"{(int)diff.TotalHours}H";
        }
        else
        {
            SyncTimeDisplay = $"{(int)diff.TotalDays}d";
        }
    }

    public void HandleLauncherTriggered()
    {
        _windowManager.ShowLauncherWindow();
    }

    private bool _disposed = false;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_timer != null)
        {
            _timer.Stop();
            _timer = null!;
        }

        _saveSubject?.Dispose();
        _saveLocalSettingsSubject?.Dispose();

        if (_keyService != null)
        {
            if (_onLauncherTriggeredHandler != null)
                _keyService.OnLauncherTriggered -= _onLauncherTriggeredHandler;
            _keyService.Dispose();
        }

        if (LocalConfig != null && _localConfigPropertyChangedHandler != null)
        {
            LocalConfig.PropertyChanged -= _localConfigPropertyChangedHandler;
        }

        if (Files != null)
        {
            Files.CollectionChanged -= OnFilesCollectionChanged;
            foreach (var item in Files)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);

        GC.SuppressFinalize(this);
    }
}
