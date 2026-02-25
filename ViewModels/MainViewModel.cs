using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Helpers;
using PromptMasterv5.Infrastructure.Services;
using PromptMasterv5.ViewModels.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using MessageBox = System.Windows.MessageBox;

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace PromptMasterv5.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDataService _dataService;
    private readonly IDataService _localDataService;
    private readonly GlobalKeyService _keyService;
    private readonly IAiService _aiService;
    private readonly IDialogService _dialogService;
    private readonly ClipboardService _clipboardService;
    private readonly IWindowManager _windowManager; // Injected
    private readonly ISettingsService _settingsService;
    private readonly ICommandExecutionService _commandExecutionService;
    private readonly ThemeService _themeService;
    private readonly HotkeyService _hotkeyService;

    // 编译后的正则表达式，用于解析变量 {{xxx}}
    private static readonly Regex VariableRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);

    private DispatcherTimer _timer;
    private readonly Subject<System.Reactive.Unit> _saveSubject = new();
    private readonly Subject<System.Reactive.Unit> _saveLocalSettingsSubject = new();

    // Event handlers for proper unsubscription
    private EventHandler? _onVoiceControlKeyDownHandler;
    private EventHandler? _onVoiceControlTriggeredHandler;
    private EventHandler? _onLauncherTriggeredHandler;
    private PropertyChangedEventHandler? _settingsVMPropertyChangedHandler;
    private PropertyChangedEventHandler? _localConfigPropertyChangedHandler;

    private bool _previousFullMode = true;
    private bool _isSimulatingKeys;
    public void SetSimulatingKeys(bool value) => _isSimulatingKeys = value;

    public SidebarViewModel SidebarVM { get; }
    public ChatViewModel ChatVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public ExternalToolsViewModel ExternalToolsVM { get; }


    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private LocalSettings localConfig;
    [ObservableProperty] private bool isFullMode = true;

    // 委托属性：转发到 SettingsViewModel（保持 XAML 兼容性）
    public bool IsSettingsOpen
    {
        get => SettingsVM?.IsSettingsOpen ?? false;
        set { if (SettingsVM != null) SettingsVM.IsSettingsOpen = value; }
    }

    public int SelectedSettingsTab
    {
        get => SettingsVM?.SelectedSettingsTab ?? 0;
        set { if (SettingsVM != null) SettingsVM.SelectedSettingsTab = value; }
    }

    public bool IsNavigationVisible
    {
        get => SettingsVM?.IsNavigationVisible ?? true;
        set { if (SettingsVM != null) SettingsVM.IsNavigationVisible = value; }
    }

    public bool IsRestoreConfirmVisible
    {
        get => SettingsVM?.IsRestoreConfirmVisible ?? false;
        set { if (SettingsVM != null) SettingsVM.IsRestoreConfirmVisible = value; }
    }

    public string? RestoreStatus
    {
        get => SettingsVM?.RestoreStatus;
        set { if (SettingsVM != null) SettingsVM.RestoreStatus = value; }
    }

    public System.Windows.Media.Brush RestoreStatusColor
    {
        get => SettingsVM?.RestoreStatusColor ?? System.Windows.Media.Brushes.Green;
        set { if (SettingsVM != null) SettingsVM.RestoreStatusColor = value; }
    }

    [ObservableProperty] private string syncTimeDisplay = "Now";
    [ObservableProperty] private ICollectionView? filesView;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;

    [ObservableProperty] private ObservableCollection<PromptItem> files = new();

    [ObservableProperty] private PromptItem? selectedFile;

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        OnPropertyChanged(nameof(HasVariables));
        OnPropertyChanged(nameof(Variables));
        IsEditMode = false; // Always default to preview mode when selecting a file
        ParseVariablesRealTime(value?.Content ?? "");
    }

    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";

    [ObservableProperty] private bool isDirty;


    public MainViewModel(
        ISettingsService settingsService,
        SettingsViewModel settingsVM,
        IAiService aiService,
        WebDavDataService dataService,
        FileDataService localDataService,
        GlobalKeyService keyService,
        ChatViewModel chatVM,
        SidebarViewModel sidebarVM,
        ExternalToolsViewModel externalToolsVM,
        IDialogService dialogService,
        ClipboardService clipboardService,
          IWindowManager windowManager,
          ICommandExecutionService commandExecutionService) // Injected
    {
        _aiService = aiService;
        _dataService = dataService;
        _localDataService = localDataService;
        _keyService = keyService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _settingsService = settingsService;
        _settingsService = settingsService;
        _windowManager = windowManager; // Assigned
        _commandExecutionService = commandExecutionService;
        _themeService = new ThemeService();
        _hotkeyService = new HotkeyService();
        
        _commandExecutionService.CommandsChanged += (_, __) => 
        {
             // Mark as dirty so sync prompt appears
             Application.Current.Dispatcher.Invoke(() => RequestSave());
        };

        SidebarVM = sidebarVM;
        ChatVM = chatVM;
        SettingsVM = settingsVM;
        ExternalToolsVM = externalToolsVM;
        ExternalToolsVM.SetMainViewModel(this);

        // 通过 SettingsService 获取配置（而不是直接加载）
        Config = settingsService.Config;
        LocalConfig = settingsService.LocalConfig;

        // 设置双向引用（SettingsVM 需要访问 MainVM 的一些数据）
        SettingsVM.SetMainViewModel(this);

        // 订阅 SettingsVM 的属性变更，传播到 MainVM 的委托属性
        _settingsVMPropertyChangedHandler = (sender, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsVM.IsSettingsOpen):
                    OnPropertyChanged(nameof(IsSettingsOpen));
                    break;
                case nameof(SettingsVM.SelectedSettingsTab):
                    OnPropertyChanged(nameof(SelectedSettingsTab));
                    OnPropertyChanged(nameof(SelectedSettingsTabName));
                    break;
                case nameof(SettingsVM.IsNavigationVisible):
                    OnPropertyChanged(nameof(IsNavigationVisible));
                    break;
                case nameof(SettingsVM.IsRestoreConfirmVisible):
                    OnPropertyChanged(nameof(IsRestoreConfirmVisible));
                    break;
                case nameof(SettingsVM.RestoreStatus):
                    OnPropertyChanged(nameof(RestoreStatus));
                    break;
                case nameof(SettingsVM.RestoreStatusColor):
                    OnPropertyChanged(nameof(RestoreStatusColor));
                    break;
            }
        };
        SettingsVM.PropertyChanged += _settingsVMPropertyChangedHandler;

        SidebarVM.Files = Files;

        ChatVM.ConfigProvider = () => Config;
        ChatVM.LocalConfigProvider = () => LocalConfig;
        ChatVM.FilesProvider = () => Files;

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, (_, __) =>
        {
            UpdateFilesViewFilter();
            FilesView?.Refresh();
            SelectedFile = null;
        });
        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            SelectedFile = m.File;
            if (m.EnterEditMode) IsEditMode = true;
        });
        WeakReferenceMessenger.Default.Register<RequestMoveFileToFolderMessage>(this, (_, m) => MoveFileToFolder(m.File, m.TargetFolder));
        WeakReferenceMessenger.Default.Register<RequestSaveMessage>(this, (_, __) => RequestSave());


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

        _onVoiceControlKeyDownHandler = (_, __) => HandleVoiceControlKeyDown();
        _keyService.OnVoiceControlKeyDown += _onVoiceControlKeyDownHandler;

        _onVoiceControlTriggeredHandler = (_, __) => HandleVoiceControlKeyUp();
        _keyService.OnVoiceControlTriggered += _onVoiceControlTriggeredHandler;
        // Initialize hotkeys from config before starting
        _keyService.LauncherHotkeyString = Config.LauncherHotkey;
        // Initialize GlobalKeyService unconditionally to support Launcher
        try { _keyService.Start(); }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "Failed to start GlobalKeyService", "MainViewModel.ctor");
        }

        // 委托给 SettingsViewModel 处理主题和快捷键
        SettingsVM.SetMainViewModel(this);
        SettingsVM.ApplyTheme();
        UpdateWindowHotkeys(); // 使用 MainViewModel 的版本，因为它注册窗口切换快捷键
        SettingsVM.UpdateExternalToolsHotkeys(); // Initialize external tool hotkeys

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

        // 附加自动保存监听器
        Files.CollectionChanged += OnFilesCollectionChanged;
        foreach (var item in Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }

        SidebarVM.Files = Files;

        // ⚠️ 关键修复：使用同一个集合引用，而不是创建新集合
        Folders = new ObservableCollection<FolderItem>(data.Folders ?? new());
        if (Folders.Count == 0)
        {
            var defaultFolder = new FolderItem { Name = "默认" };
            Folders.Add(defaultFolder);
            SidebarVM.SelectedFolder = defaultFolder;
        }
        else
        {
            SidebarVM.SelectedFolder = Folders.FirstOrDefault();
        }

        // 将同一个集合引用赋值给 SidebarVM
        SidebarVM.Folders = Folders;

        if (SidebarVM.SelectedFolder != null)
        {
            foreach (var f in Files)
            {
                if (string.IsNullOrWhiteSpace(f.FolderId))
                {
                    f.FolderId = SidebarVM.SelectedFolder.Id;
                }
            }
        }

        // Restore voice commands from sync data
        if (data.VoiceCommands != null && data.VoiceCommands.Count > 0)
        {
            _commandExecutionService.SetCommands(data.VoiceCommands);
        }

        FilesView = CollectionViewSource.GetDefaultView(Files);
        UpdateFilesViewFilter();
        FilesView?.Refresh();

        // 自动选择第一个文件，以便 Block3 默认显示内容
        if (FilesView != null && !FilesView.IsEmpty)
        {
            var firstItem = FilesView.Cast<PromptItem>().FirstOrDefault();
            if (firstItem != null)
            {
                SelectedFile = firstItem;
            }
        }

        IsDirty = false; // Initial load is consistent with source
    }

    private void HandleVoiceControlKeyDown()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                // If already open and listening, ignore repeated KeyDown
                var existing = Application.Current.Windows.OfType<Views.VoiceControlWindow>().FirstOrDefault();
                if (existing != null) return;

                var app = Application.Current as App;
                if (app != null)
                {
                    var vm = app.ServiceProvider.GetRequiredService<VoiceControlViewModel>();
                    var window = new Views.VoiceControlWindow(vm);
                    window.Show();
                    vm.StartRecordingSession();
                }
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to start voice control", "MainViewModel.HandleVoiceControlKeyDown");
            }
        });
    }

    private void HandleVoiceControlKeyUp()
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            try
            {
                var existing = Application.Current.Windows.OfType<Views.VoiceControlWindow>().FirstOrDefault();
                if (existing == null) return;

                var vm = existing.DataContext as VoiceControlViewModel;
                if (vm != null && vm.IsListening)
                {
                    await vm.StopAndProcess();
                }
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to stop voice control", "MainViewModel.HandleVoiceControlKeyUp");
            }
        });
    }

    public void UpdateFilesViewFilter()
    {
        if (FilesView == null) return;

        var selectedFolderId = SidebarVM.SelectedFolder?.Id;
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
        _previousFullMode = true;
    }

    // Hotkey handlers (called by SettingsViewModel)
    public void OnWindowHotkeyPressed()
    {
        ToggleMainWindow();
    }

    public void ToggleModeViaHotkey()
    {
        // Mini mode removed, always ensure full mode
        EnterFullMode();
    }

    public string SelectedSettingsTabName => SelectedSettingsTab switch
    {
        0 => "基础",
        1 => "发送方式",
        2 => "外部工具",
        3 => "发送",
        4 => "高级",
        _ => "设置"
    };

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsVM.OpenSettingsCommand.Execute(null);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        SettingsVM.CloseSettingsCommand.Execute(null);
        UpdateWindowHotkeys(); // 使用 MainViewModel 的版本
    }

    [RelayCommand]
    private void SelectSettingsTab(string tabIndex)
    {
        if (int.TryParse(tabIndex, out int idx)) SelectedSettingsTab = idx;
    }

    [RelayCommand]

    private void ImportMarkdownFiles()
    {
        string filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*";
        var files = _dialogService.ShowOpenFilesDialog(filter);

        if (files == null || files.Length == 0) return;

        var targetFolder = SidebarVM.SelectedFolder;
        if (targetFolder == null)
        {
            targetFolder = new FolderItem { Name = "导入" };
            SidebarVM.Folders.Add(targetFolder);
            SidebarVM.SelectedFolder = targetFolder;
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
        var dialog = new PromptMasterv5.IconInputDialog(file.IconGeometry);
        if (dialog.ShowDialog() == true)
        {
            file.IconGeometry = dialog.ResultGeometry;
            RequestSave();
        }
    }

    [RelayCommand]
    private void ToggleNavigation()
    {
        SettingsVM.ToggleNavigationCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        SettingsVM.ToggleThemeCommand.Execute(null);
    }



    [RelayCommand]
    private void AddAiModel() => SettingsVM.AddAiModelCommand.Execute(null);

    [RelayCommand]
    private void DeleteAiModel(AiModelConfig? model) => SettingsVM.DeleteAiModelCommand.Execute(model);


    [RelayCommand]
    private void ShowRestoreConfirm() => SettingsVM.ShowRestoreConfirmCommand.Execute(null);

    [RelayCommand]
    private void CancelRestoreConfirm() => SettingsVM.CancelRestoreConfirmCommand.Execute(null);

    [RelayCommand]
    private void ManualRestore() => SettingsVM.ManualRestoreCommand.Execute(null);

    [RelayCommand]
    private void ManualLocalRestore() => SettingsVM.ManualLocalRestoreCommand.Execute(null);


    public async Task BackupToCloudAsync()
    {
        if (SettingsVM?.ManualBackupCommand is IAsyncRelayCommand cmd)
        {
            await cmd.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void ManualBackup() => SettingsVM.ManualBackupCommand.Execute(null);

    [RelayCommand]
    private void OpenLogFolder() => SettingsVM.OpenLogFolderCommand.Execute(null);

    [RelayCommand]
    private void ClearLogs() => SettingsVM.ClearLogsCommand.Execute(null);

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
            SelectedFile.LastModified = DateTime.Now;
            IsEditMode = false;
            RequestSave();
            return;
        }

        IsEditMode = true;
    }

    [RelayCommand]
    private void CopyCompiledText()
    {
        var text = CompileContent();
        if (string.IsNullOrWhiteSpace(text)) return;
        _clipboardService.SetClipboard(text);
    }

    [RelayCommand]
    private async Task SendDefaultWebTarget()
    {
        if (SelectedFile == null) return;
        var targetName = Config.DefaultWebTargetName;
        var target = Config.WebDirectTargets.FirstOrDefault(t => t.Name == targetName);

        if (target != null)
        {
            if (!target.IsEnabled)
            {
                _dialogService.ShowAlert($"默认目标 '{targetName}' 已被禁用，请在设置中启用。", "目标不可用");
                return;
            }
            await OpenWebTarget(target).ConfigureAwait(false);
        }
        else
        {
            // Fallback: try finding Gemini or first available
            target = Config.WebDirectTargets.FirstOrDefault(t => t.Name == "Gemini" && t.IsEnabled)
                     ?? Config.WebDirectTargets.FirstOrDefault(t => t.IsEnabled);

            if (target != null)
            {
                await OpenWebTarget(target).ConfigureAwait(false);
            }
            else
            {
                _dialogService.ShowAlert($"未找到默认网页目标: {targetName}，且无可用的备选目标。", "配置错误");
            }
        }
    }

    [RelayCommand]
    private async Task OpenWebTarget(WebTarget? target)
    {
        if (target == null || SelectedFile == null) return;

        // 1. Check Variables (if any are empty, stop)
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

        // 2. Compile Content
        var content = CompileContent();
        if (string.IsNullOrWhiteSpace(content))
        {
            _dialogService.ShowAlert("提示词内容为空。", "无法打开");
            return;
        }

        // 3. Determine URL strategy
        bool supportsUrlParam = target.UrlTemplate.Contains("{0}");
        bool useClipboard = !supportsUrlParam || content.Length > 2000;
        bool autoPaste = !supportsUrlParam; // Auto Ctrl+V for clipboard-only targets (e.g. Gemini)
        string url;

        try
        {
            if (useClipboard)
            {
                // Copy to clipboard
                _clipboardService.SetClipboard(content);

                if (supportsUrlParam)
                {
                    // Has {0} but content too long, strip query
                    try { url = string.Format(target.UrlTemplate, ""); }
                    catch { url = target.UrlTemplate.Split('?')[0]; }
                    _dialogService.ShowAlert("提示词过长，已复制到剪贴板，请手动粘贴。", "提示");
                }
                else
                {
                    // No {0} placeholder — use URL as-is (e.g. Gemini)
                    url = target.UrlTemplate;
                    // No dialog here — we will auto-paste after delay
                }
            }
            else
            {
                // URL Encode and format
                url = string.Format(target.UrlTemplate, System.Uri.EscapeDataString(content));
            }

            // 4. Open Browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            // 5. Hide Window (to Tray)
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Hide();
            }

            // 6. Auto-paste is no longer needed as we use Userscript to handle ?q= parameter
            // The Userscript will intercept the URL, extract the prompt, and fill the input box.

            // 7. Clear Input
            AdditionalInput = "";
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "OpenWebTarget Failed", "MainViewModel");
            _dialogService.ShowAlert($"打开网页失败: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private void SearchOnGitHub()
    {
        // 1. Get content from Block4 input only
        var query = AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return;
        }

        try
        {
            // 2. Construct GitHub Search URL
            var url = $"https://github.com/search?q={System.Uri.EscapeDataString(query)}";

            // 3. Open Browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            // 4. Clear input (consistent with other "send" actions)
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
        var win = Application.Current.MainWindow;
        if (win == null) return;

        if (win.Visibility != Visibility.Visible)
        {
            IsFullMode = true;
            win.Show();
            win.Activate();
            return;
        }

        _previousFullMode = IsFullMode;
        win.Hide();
    }

    private void ApplyTheme(ThemeType theme)
    {
        _themeService.ApplyTheme(theme);
    }

    private static string NormalizeSymbols(string s) =>
        StringUtils.NormalizeSymbols(s);

    // ... (existing code) ...





    private void ParseVariablesRealTime(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            Variables.Clear();
            HasVariables = false;
            return;
        }

        var matches = VariableRegex.Matches(content);
        var newVarNames = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        for (int i = Variables.Count - 1; i >= 0; i--)
        {
            if (!newVarNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
        }

        foreach (var name in newVarNames)
        {
            if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
        }

        HasVariables = Variables.Count > 0;
    }



    [SupportedOSPlatform("windows")]
    public void UpdateWindowHotkeys()
    {
        _hotkeyService.RegisterWindowHotkey("ToggleFullWindowHotkey", Config.FullWindowHotkey, () => ToggleWindowToMode(true));
        
        // 注册启动条显示/隐藏快捷键
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
            var voiceCommandService = (Application.Current as App)?.ServiceProvider.GetRequiredService<ICommandExecutionService>();
            var voiceCommands = voiceCommandService?.GetCommands() ?? new Dictionary<string, string>();
            await _localDataService.SaveAsync(Folders, Files, voiceCommands);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "Failed to perform local backup", "MainViewModel.PerformLocalBackup");
        }
    }

    /// <summary>
    /// 当 Files 集合发生变化时（添加/删除项目），附加或移除监听器并触发保存
    /// </summary>
    private void OnFilesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // 为新添加的项目附加监听器
        if (e.NewItems != null)
        {
            foreach (PromptItem item in e.NewItems)
            {
                item.PropertyChanged += OnFilePropertyChanged;
            }
        }

        // 从移除的项目上卸载监听器
        if (e.OldItems != null)
        {
            foreach (PromptItem item in e.OldItems)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        // 集合变化时触发保存
        RequestSave();
    }

    /// <summary>
    /// 当 PromptItem 的属性发生变化时触发保存
    /// </summary>
    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 避免因更新 LastModified 导致的循环触发
        if (e.PropertyName == nameof(PromptItem.LastModified))
            return;

        if (e.PropertyName == nameof(PromptItem.Content) && sender == SelectedFile)
        {
            ParseVariablesRealTime(SelectedFile?.Content ?? "");
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

    private string CompileContent()
    {
        string finalContent = SelectedFile?.Content ?? "";

        if (HasVariables)
        {
            foreach (var variable in Variables)
            {
                finalContent = finalContent.Replace("{{" + variable.Name + "}}", variable.Value ?? "");
            }
        }

        if (!string.IsNullOrWhiteSpace(AdditionalInput))
        {
            if (!string.IsNullOrWhiteSpace(finalContent)) finalContent += "\n";
            finalContent += AdditionalInput;
        }

        return finalContent;
    }


    [RelayCommand]
    private void TriggerOcr() => ExternalToolsVM.TriggerOcrCommand.Execute(null);

    [RelayCommand]
    private void TriggerTranslate() => ExternalToolsVM.TriggerTranslateCommand.Execute(null);

    public void HandleLauncherTriggered()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Close any existing launcher windows to avoid stacking
            foreach (Window w in Application.Current.Windows)
            {
                if (w is Views.LauncherWindow)
                {
                    w.Activate();
                    w.Focus();
                    return;
                }
            }

            if (Application.Current is not App app) return;
            
            var vm = app.ServiceProvider.GetRequiredService<LauncherViewModel>();
            var win = new Views.LauncherWindow
            {
                DataContext = vm
            };

            vm.RequestClose = () => win.Close();

            win.Show();
            win.Activate();
            win.Focus();
        });
    }


    // ========== IDisposable ==========
    private bool _disposed = false;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop and dispose timer
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null!;
        }

        // Dispose Reactive Subjects
        _saveSubject?.Dispose();
        _saveLocalSettingsSubject?.Dispose();

        // Unsubscribe from GlobalKeyService events and dispose
        if (_keyService != null)
        {
            if (_onLauncherTriggeredHandler != null)
                _keyService.OnLauncherTriggered -= _onLauncherTriggeredHandler;
            if (_onVoiceControlKeyDownHandler != null)
                _keyService.OnVoiceControlKeyDown -= _onVoiceControlKeyDownHandler;
            if (_onVoiceControlTriggeredHandler != null)
                _keyService.OnVoiceControlTriggered -= _onVoiceControlTriggeredHandler;
            _keyService.Dispose();
        }

        // Unsubscribe from SettingsVM PropertyChanged
        if (SettingsVM != null && _settingsVMPropertyChangedHandler != null)
        {
            SettingsVM.PropertyChanged -= _settingsVMPropertyChangedHandler;
        }

        // Unsubscribe from LocalConfig PropertyChanged
        if (LocalConfig != null && _localConfigPropertyChangedHandler != null)
        {
            LocalConfig.PropertyChanged -= _localConfigPropertyChangedHandler;
        }

        // Unsubscribe from Files collection events
        if (Files != null)
        {
            Files.CollectionChanged -= OnFilesCollectionChanged;
            foreach (var item in Files)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        // Unregister WeakReferenceMessenger
        WeakReferenceMessenger.Default.UnregisterAll(this);

        // Dispose services
        _commandExecutionService?.Dispose();

        GC.SuppressFinalize(this);
    }
}
