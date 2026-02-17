using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using NHotkey;
using NHotkey.Wpf;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
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
using System.Windows.Input;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using InputMode = PromptMasterv5.Core.Models.InputMode;
using MessageBox = System.Windows.MessageBox;

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace PromptMasterv5.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDataService _localDataService;
    private readonly GlobalKeyService _keyService;
    private readonly IAiService _aiService;
    private readonly FabricService _fabricService;
    private readonly IDialogService _dialogService;
    private readonly ClipboardService _clipboardService;
    private readonly WindowPositionService _windowPositionService;
    private readonly IWindowManager _windowManager; // Injected
    private readonly ISettingsService _settingsService;

    private DispatcherTimer _timer;
    private readonly Subject<System.Reactive.Unit> _saveSubject = new();
    private readonly Subject<System.Reactive.Unit> _saveLocalSettingsSubject = new();
    private bool _previousFullMode = true;
    private IntPtr _previousWindowHandle = IntPtr.Zero;
    private bool _isSimulatingKeys;
    public void SetSimulatingKeys(bool value) => _isSimulatingKeys = value;

    public SidebarViewModel SidebarVM { get; }
    public ChatViewModel ChatVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public ExternalToolsViewModel ExternalToolsVM { get; }

    public IDropTarget MiniPinnedPromptDropHandler { get; private set; }

    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private LocalSettings localConfig;
    [ObservableProperty] private bool isFullMode = true;
    [ObservableProperty] private bool isMiniVarsExpanded;
    
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

    partial void OnFilesChanged(ObservableCollection<PromptItem> value)
    {
        if (IsGlobalPromptListOpen) UpdateGlobalPromptList();
    }

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

    [ObservableProperty] private bool isGlobalPromptListOpen;
    [ObservableProperty] private ObservableCollection<PromptGroup> globalPromptList = new();

    public MainViewModel(
        ISettingsService settingsService,
        SettingsViewModel settingsVM,
        IAiService aiService,
        WebDavDataService dataService,
        FileDataService localDataService,
        GlobalKeyService keyService,
        FabricService fabricService,
        ChatViewModel chatVM,
        SidebarViewModel sidebarVM,
        ExternalToolsViewModel externalToolsVM,
        IDialogService dialogService,
        ClipboardService clipboardService,
        WindowPositionService windowPositionService,
        IWindowManager windowManager) // Added parameter
    {
        _aiService = aiService;
        _dataService = dataService;
        _localDataService = localDataService;
        _keyService = keyService;
        _fabricService = fabricService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _windowPositionService = windowPositionService;
        _settingsService = settingsService;
        _windowManager = windowManager; // Assigned
        
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
        SettingsVM.PropertyChanged += (sender, e) =>
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

        SidebarVM.Files = Files;

        ChatVM.ConfigProvider = () => Config;
        ChatVM.LocalConfigProvider = () => LocalConfig;
        ChatVM.FilesProvider = () => Files;

        WeakReferenceMessenger.Default.Register<MiniInputTextChangedMessage>(this, (_, m) => HandleMiniInputTextChangedFromChat(m.Value));
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

        MiniPinnedPromptDropHandler = new PinnedPromptDropHandler(this);

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

        LocalConfig.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocalSettings.Block1Width) || 
                e.PropertyName == nameof(LocalSettings.Block2Width))
            {
                _saveLocalSettingsSubject.OnNext(System.Reactive.Unit.Default);
            }
        };

        _keyService.OnDoubleCtrlDetected += (_, __) => Application.Current.Dispatcher.Invoke(() => 
        {
            if (_isSimulatingKeys) return;
            ToggleMainWindow();
        });
        _keyService.OnDoubleSemiColonDetected += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            if (!IsFullMode) ChatVM.MiniInputText = "";
        });
        _keyService.OnQuickActionTriggered += async (_, __) => await HandleQuickActionTriggered();
        if (Config.EnableDoubleCtrl) try { _keyService.Start(); } catch { }

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

        SyncMiniPinnedPrompts();
        IsDirty = false; // Initial load is consistent with source
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

    [RelayCommand]
    private void ExitFullMode()
    {
        IsFullMode = false;
        _previousFullMode = false;
    }

    // Hotkey handlers (called by SettingsViewModel)
    public void OnWindowHotkeyPressed()
    {
        ToggleMainWindow();
    }

    public void ToggleModeViaHotkey()
    {
        if (IsFullMode)
        {
            ExitFullMode();
        }
        else
        {
            EnterFullMode();
        }
    }

    public string SelectedSettingsTabName =>SelectedSettingsTab switch
    {
        0 => "基础",
        1 => "发送方式",
        2 => "外部工具",
        3 => "发送",
        4 => "迷你",
        5 => "高级",
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

    // Settings Configuration Delegates
    public Task<(bool Success, string Message)> TestAiConnectionAsync() => SettingsVM.TestAiConnectionAsync();
    public Task<(bool Success, string Message)> TestAiTranslationConnectionAsync() => SettingsVM.TestAiTranslationConnectionAsync();

    [RelayCommand]
    private void AddAiModel() => SettingsVM.AddAiModelCommand.Execute(null);

    [RelayCommand]
    private void DeleteAiModel(AiModelConfig? model) => SettingsVM.DeleteAiModelCommand.Execute(model);

    [RelayCommand]
    private void ActivateAiModel(AiModelConfig? model) => SettingsVM.ActivateAiModelCommand.Execute(model);

    [RelayCommand]
    private void ShowRestoreConfirm() => SettingsVM.ShowRestoreConfirmCommand.Execute(null);

    [RelayCommand]
    private void CancelRestoreConfirm() => SettingsVM.CancelRestoreConfirmCommand.Execute(null);

    [RelayCommand]
    private void ManualRestore() => SettingsVM.ManualRestoreCommand.Execute(null);

    [RelayCommand]
    private void ManualLocalRestore() => SettingsVM.ManualLocalRestoreCommand.Execute(null);

    [RelayCommand]
    private void TriggerQuickAction()
    {
        var window = new Views.CaptureWindow();
        window.Show();
        window.Activate();
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



    private void ToggleMainWindow()
    {
        var win = Application.Current.MainWindow;
        if (win == null) return;

        if (win.Visibility != Visibility.Visible)
        {
            IsFullMode = _previousFullMode;
            win.Show();
            win.Activate();
            if (!IsFullMode) win.Topmost = true;
            return;
        }

        _previousFullMode = IsFullMode;
        win.Hide();
    }

    private static void SetBrush(ResourceDictionary resources, string key, string color)
    {
        static System.Windows.Media.Color ParseColor(string value) =>
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);

        resources[key] = new System.Windows.Media.SolidColorBrush(ParseColor(color));
    }

    private void ApplyTheme(ThemeType theme)
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;

        if (theme == ThemeType.Dark)
        {
            SetBrush(resources, "ShellBackground", "#2E3033");
            SetBrush(resources, "AppBackground", "#363B40");
            SetBrush(resources, "SidebarBackground", "#2E3033");
            SetBrush(resources, "CardBackground", "#363B40");
            SetBrush(resources, "TextPrimary", "#ACBFBE");
            SetBrush(resources, "TextSecondary", "#ACBFBE");
            SetBrush(resources, "DividerColor", "#4A4F55");
            SetBrush(resources, "HintBrush", "#ACBFBE");
            SetBrush(resources, "ListItemHoverBackgroundBrush", "#3A3F45");
            SetBrush(resources, "ListItemSelectedBackgroundBrush", "#444A52");
            SetBrush(resources, "InputFocusBackgroundBrush", "#2E3033");

            SetBrush(resources, "Block1Background", "#2E3033");
            SetBrush(resources, "Block2Background", "#2E3033");
            SetBrush(resources, "Block3Background", "#363B40");
            SetBrush(resources, "Block4Background", "#363B40");
            SetBrush(resources, "PrimaryTextBrush", "#ACBFBE");
            SetBrush(resources, "SecondaryTextBrush", "#ACBFBE");
            SetBrush(resources, "PlaceholderTextBrush", "#ACBFBE");
            SetBrush(resources, "DividerBrush", "#4A4F55");
            SetBrush(resources, "ActionIconBrush", "#ACBFBE");
            SetBrush(resources, "ActionIconHoverBrush", "#ACBFBE");
            SetBrush(resources, "HeaderIconBrush", "#ACBFBE");
            SetBrush(resources, "HeaderIconHoverBrush", "#ACBFBE");

            SetBrush(resources, "MiniCaretBrush", "#B8BFC6");
            SetBrush(resources, "Block3EditorTextBrush", "#B8BFC6");
            SetBrush(resources, "Block3EditorCaretBrush", "#B8BFC6");
            SetBrush(resources, "Block3EditorSelectionBrush", "#4A89DC");
        }
        else
        {
            SetBrush(resources, "ShellBackground", "#FAFAFA");
            SetBrush(resources, "AppBackground", "#F1F1EF");
            SetBrush(resources, "SidebarBackground", "#F7F7F7");
            SetBrush(resources, "CardBackground", "#FFFFFF");
            SetBrush(resources, "TextPrimary", "#333333");
            SetBrush(resources, "TextSecondary", "#666666");
            SetBrush(resources, "DividerColor", "#E5E5E5");
            SetBrush(resources, "HintBrush", "#999999");
            SetBrush(resources, "ListItemHoverBackgroundBrush", "#EAEAEA");
            SetBrush(resources, "ListItemSelectedBackgroundBrush", "#E0E0E0");
            SetBrush(resources, "InputFocusBackgroundBrush", "#FFFFFF");

            SetBrush(resources, "Block1Background", "#E8E7E7");
            SetBrush(resources, "Block2Background", "#E8E7E7");
            SetBrush(resources, "Block3Background", "#EDEDED");
            SetBrush(resources, "Block4Background", "#EDEDED");
            SetBrush(resources, "PrimaryTextBrush", "#333333");
            SetBrush(resources, "SecondaryTextBrush", "#666666");
            SetBrush(resources, "PlaceholderTextBrush", "#999999");
            SetBrush(resources, "DividerBrush", "#E5E5E5");
            SetBrush(resources, "ActionIconBrush", "#666666");
            SetBrush(resources, "ActionIconHoverBrush", "#333333");
            SetBrush(resources, "HeaderIconBrush", "#666666");
            SetBrush(resources, "HeaderIconHoverBrush", "#333333");

            SetBrush(resources, "MiniCaretBrush", "#333333");
            SetBrush(resources, "Block3EditorTextBrush", "#333333");
            SetBrush(resources, "Block3EditorCaretBrush", "#333333");
            SetBrush(resources, "Block3EditorSelectionBrush", "#4A89DC");
        }
    }

    private static char NormalizeSymbol(char c)
    {
        return c switch
        {
            '；' => ';',
            '＇' => '\'',
            '‘' => '\'',
            '’' => '\'',
            '`' => '\'',
            '´' => '\'',
            _ => c
        };
    }

    private static string NormalizeSymbols(string s) =>
        new string((s ?? "").Select(NormalizeSymbol).ToArray());

    // ... (existing code) ...



    private void HandleMiniInputTextChangedFromChat(string value)
    {
        if (IsGlobalPromptListOpen)
        {
            UpdateGlobalPromptList(value);
        }

        if (!LocalConfig.MiniAiOnlyChatEnabled)
        {
            Variables.Clear();
            HasVariables = false;
            IsMiniVarsExpanded = false;

            if (string.IsNullOrWhiteSpace(value))
            {
                ChatVM.IsSearchPopupOpen = false;
                return;
            }

            // Only show search popup if Global List is NOT open to avoid clutter
            if (!IsGlobalPromptListOpen)
            {
                ChatVM.UpdateSearchPopup(value.Trim());
            }
            else
            {
                ChatVM.IsSearchPopupOpen = false; 
            }
            return;
        }

        var prefix = LocalConfig.MiniPatternPrefix ?? "";
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            var normalizedValue = NormalizeSymbols(value);
            var normalizedPrefix = NormalizeSymbols(prefix);
            if (normalizedValue.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            {
                ChatVM.IsSearchPopupOpen = false;
                Variables.Clear();
                HasVariables = false;
                IsMiniVarsExpanded = false;
                return;
            }
        }

        ChatVM.IsSearchPopupOpen = false;
        ParseVariablesRealTime(value ?? "");
    }


    private void ParseVariablesRealTime(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            Variables.Clear();
            HasVariables = false;
            IsMiniVarsExpanded = false;
            return;
        }

        var matches = Regex.Matches(content, @"\{\{(.*?)\}\}");
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
        if (!IsFullMode) IsMiniVarsExpanded = HasVariables;
    }


    [RelayCommand]
    private void EnableMiniAiMode()
    {
        LocalConfig.MiniAiOnlyChatEnabled = true;
        if (string.IsNullOrWhiteSpace(LocalConfig.MiniPatternPrefix)) LocalConfig.MiniPatternPrefix = "ai";
        LocalConfigService.Save(LocalConfig);
    }

    [RelayCommand]
    private void EnableMiniTestMode()
    {
        LocalConfig.MiniAiOnlyChatEnabled = false;
        LocalConfigService.Save(LocalConfig);
    }

    private static void RegisterWindowHotkey(string name, string hotkeyStr, Action action)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hotkeyStr))
            {
                try { HotkeyManager.Current.Remove(name); } catch { }
                return;
            }

            ModifierKeys modifiers = ModifierKeys.None;
            if (hotkeyStr.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
            if (hotkeyStr.Contains("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
            if (hotkeyStr.Contains("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
            if (hotkeyStr.Contains("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Windows;

            string keyStr = hotkeyStr.Split('+').Last().Trim();
            if (Enum.TryParse(keyStr, true, out Key key))
            {
                try { HotkeyManager.Current.Remove(name); } catch { }
                HotkeyManager.Current.AddOrReplace(name, key, modifiers, (_, __) => action());
            }
        }
        catch { }
    }

    [SupportedOSPlatform("windows")]
    public void UpdateWindowHotkeys()
    {
        try { HotkeyManager.Current.Remove("ToggleWindow"); } catch { }
        try { HotkeyManager.Current.Remove("ToggleWindowSingle"); } catch { }

        RegisterWindowHotkey("ToggleFullWindowHotkey", Config.FullWindowHotkey, () => ToggleWindowToMode(true));
        RegisterWindowHotkey("ToggleMiniWindowHotkey", Config.MiniWindowHotkey, () => ToggleWindowToMode(false));
    }

    private void ToggleWindowToMode(bool targetFull)
    {
        var win = Application.Current.MainWindow;
        if (win == null) return;

        if (win.Visibility != Visibility.Visible)
        {
            IsFullMode = targetFull;
            win.Show();
            win.Activate();
            if (!targetFull) win.Topmost = true;
            return;
        }

        if (IsFullMode != targetFull)
        {
            IsFullMode = targetFull;
            win.Activate();
            if (!targetFull) win.Topmost = true;
            return;
        }

        win.Hide();
    }



    public void AddMiniPinnedPromptFromCandidate()
    {
        ChatVM.AddMiniPinnedPromptFromCandidate(LocalConfig.MiniPinnedPromptIds, LocalConfig.MiniPinnedPromptCandidateId ?? "");
        LocalConfigService.Save(LocalConfig);
    }

    [RelayCommand]
    private void RemoveMiniPinnedPrompt(PromptItem? prompt)
    {
        if (prompt == null) return;
        RemoveMiniPinnedPromptById(prompt.Id);
    }

    public void RemoveMiniPinnedPromptById(string id)
    {
        ChatVM.RemoveMiniPinnedPromptById(
            LocalConfig.MiniPinnedPromptIds,
            clearSelectedPinnedIfMatch: () =>
            {
                if (LocalConfig.MiniSelectedPinnedPromptId == id) LocalConfig.MiniSelectedPinnedPromptId = "";
            },
            id: id);
        LocalConfigService.Save(LocalConfig);
    }

    public void ReorderMiniPinnedPrompts(int oldIndex, int newIndex) =>
        ChatVM.ReorderMiniPinnedPrompts(LocalConfig.MiniPinnedPromptIds, oldIndex, newIndex);

    public void SyncMiniPinnedPrompts() =>
        ChatVM.SyncMiniPinnedPrompts(LocalConfig.MiniPinnedPromptIds, LocalConfig.MiniSelectedPinnedPromptId);

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
        catch { }
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
        string finalContent;
        if (IsFullMode)
        {
            finalContent = SelectedFile?.Content ?? "";
        }
        else
        {
            finalContent = ChatVM.MiniInputText;
            if (!LocalConfig.MiniPinnedPromptClickShowsFullContent && !string.IsNullOrWhiteSpace(LocalConfig.MiniSelectedPinnedPromptId))
            {
                var pinned = Files.FirstOrDefault(f => f.Id == LocalConfig.MiniSelectedPinnedPromptId);
                var pinnedContent = pinned?.Content ?? "";
                if (!string.IsNullOrWhiteSpace(pinnedContent))
                {
                    if (string.IsNullOrWhiteSpace(finalContent))
                    {
                        finalContent = pinnedContent;
                    }
                    else
                    {
                        finalContent = $"{pinnedContent}\n\n---\n\nUSER INPUT:\n{finalContent}";
                    }
                }
            }
        }

        if (HasVariables)
        {
            foreach (var variable in Variables)
            {
                finalContent = finalContent.Replace("{{" + variable.Name + "}}", variable.Value ?? "");
            }
        }

        if (IsFullMode && !string.IsNullOrWhiteSpace(AdditionalInput))
        {
            if (!string.IsNullOrWhiteSpace(finalContent)) finalContent += "\n";
            finalContent += AdditionalInput;
        }

        return finalContent;
    }

    public async Task SendBySmartFocus()
    {
        string content = CompileContent();
        await ExecuteSendAsync(content, InputMode.SmartFocus);
        if (IsFullMode) AdditionalInput = "";
        else ChatVM.MiniInputText = "";
    }

    public async Task SendByCoordinate()
    {
        string content = CompileContent().TrimEnd();
        await ExecuteSendAsync(content, InputMode.CoordinateClick);
        if (IsFullMode) AdditionalInput = "";
        else ChatVM.MiniInputText = "";
    }

    [RelayCommand]
    private async Task SendFromMini(string modeStr)
    {
        if (modeStr == "Coordinate") await SendByCoordinate();
        else await SendBySmartFocus();
    }

    private async Task ExecuteSendAsync(string content, InputMode targetMode)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var window = Application.Current.MainWindow;
        bool wasMiniMode = !IsFullMode;

        bool stoppedGlobalHook = false;
        try
        {
            if (Config.EnableDoubleCtrl)
            {
                _keyService.Stop();
                stoppedGlobalHook = true;
            }

            if (window != null) window.Hide();

            await InputSender.SendAsync(content, targetMode, LocalConfig, _previousWindowHandle);
        }
        finally
        {
            if (stoppedGlobalHook)
            {
                await Task.Delay(450);
                if (Config.EnableDoubleCtrl)
                {
                    try { _keyService.Start(); } catch { }
                }
            }
        }

        if (!IsFullMode) 
        {
            ChatVM.MiniInputText = "";
            LocalConfig.MiniSelectedPinnedPromptId = "";
        }
        else AdditionalInput = "";

        if (wasMiniMode && window != null)
        {
            window.Show();
            window.Activate();
            window.Topmost = true;
            if (window is MainWindow mainWin) mainWin.MiniInputBox.Focus();
        }
    }




    [RelayCommand]
    private void TriggerSelectedTextTranslate() => ExternalToolsVM.TriggerSelectedTextTranslateCommand.Execute(null);

    [RelayCommand]
    private void TriggerOcr() => ExternalToolsVM.TriggerOcrCommand.Execute(null);

    [RelayCommand]
    private void TriggerTranslate() => ExternalToolsVM.TriggerTranslateCommand.Execute(null);

    // ========== 全局划词助手 ==========
    private async Task HandleQuickActionTriggered()
    {
        try
        {
            // 1. 获取选中文本
            var selectedText = await _clipboardService.GetSelectedTextAsync();
            
            // 1.1 清理文本 (移除不可见字符、控制符)
            if (selectedText != null)
            {
                selectedText = selectedText.Trim().Trim('\0', '\uFEFF', '\u200B');
            }

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                // 优化：使用统一的 TranslationPopup 展示警告
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _windowManager.ShowTranslationPopup("未能获取选中文本...");
                });
                return;
            }

            // 2. 获取带有 IsQuickAction 标记的提示词
            var quickActions = new ObservableCollection<PromptItem>();

            // 优先使用 LocalConfig 中的配置 (支持自定义模型绑定)
            if (LocalConfig.QuickActionPrompts != null && LocalConfig.QuickActionPrompts.Count > 0)
            {
                foreach (var configItem in LocalConfig.QuickActionPrompts)
                {
                    var file = Files.FirstOrDefault(f => f.Id == configItem.Id);
                    if (file != null)
                    {
                        // 创建临时对象，确保使用配置中的模型ID，且不污染原始对象
                        quickActions.Add(new PromptItem
                        {
                            Id = file.Id,
                            Title = file.Title,
                            Content = file.Content,
                            FolderId = file.FolderId,
                            IconGeometry = file.IconGeometry,
                            Description = file.Description,
                            IsQuickAction = true,
                            BoundModelId = configItem.BoundModelId,
                            CreatedAt = file.CreatedAt,
                            LastModified = file.LastModified
                        });
                    }
                }
            }

            // 如果配置为空，回退到旧版 IsQuickAction 标记逻辑
            if (quickActions.Count == 0)
            {
                var legacyItems = Files.Where(f => f.IsQuickAction).ToList();
                
                // 如果为空，尝试自动初始化默认指令
                if (legacyItems.Count == 0)
                {
                    InitializeQuickActions();
                    legacyItems = Files.Where(f => f.IsQuickAction).ToList();
                }
                
                foreach (var item in legacyItems) quickActions.Add(item);
            }

            if (quickActions.Count == 0)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _dialogService.ShowAlert("未配置快捷指令。\n请在设置中将提示词标记为快捷操作。", "提示");
                });
                return;
            }

            // 3. 创建 ViewModel 和窗口
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var viewModel = new QuickActionViewModel(
                    _aiService,
                    _clipboardService,
                    _settingsService,
                    selectedText,
                    quickActions);

                var window = new Views.QuickActionWindow
                {
                    DataContext = viewModel
                };

                // 定位窗口
                _windowPositionService.PositionWindowNearMouse(window);

                // 显示窗口
                window.Show();
                window.Activate();
            });
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"快捷助手触发失败: {ex.Message}", "MainViewModel.HandleQuickActionTriggered");
        }
    }

    [RelayCommand]
    private void ToggleQuickAction(PromptItem? file)
    {
        if (file == null) return;
        file.IsQuickAction = !file.IsQuickAction;
        RequestSave();
    }

    private void InitializeQuickActions()
    {
        // 如果已经有配置的快捷指令，直接返回
        if (Files.Any(f => f.IsQuickAction)) return;

        // 尝试自动标记一些常用指令
        bool modified = false;
        var commonKeywords = new[] { "润色", "Polish", "翻译", "Translate", "解释", "Explain", "摘要", "Summarize" };

        foreach (var keyword in commonKeywords)
        {
            var match = Files.FirstOrDefault(f => f.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (match != null && !match.IsQuickAction)
            {
                match.IsQuickAction = true;
                modified = true;
            }
        }

        // 如果没有找到匹配的，或者列表为空，我们什么都不做，等待用户手动添加
        // 但如果有修改，保存
        if (modified) RequestSave();
    }

    [RelayCommand]
    private void ToggleGlobalPromptList()
    {
        // IsGlobalPromptListOpen is toggled by the UI binding (ToggleButton.IsChecked)
        // We only need to react to the state change
        if (IsGlobalPromptListOpen)
        {
            // Update with partial or empty filter
            UpdateGlobalPromptList(ChatVM?.MiniInputText ?? "");
        }
    }

    [RelayCommand]
    private void SelectGlobalPrompt(PromptItem prompt)
    {
        if (prompt == null) return;
        
        IsGlobalPromptListOpen = false;

        string textToInsert = ChatVM.MiniInputText ?? "";
        if (LocalConfig.MiniPinnedPromptClickShowsFullContent)
        {
             // Full Content Mode: Show Text ONLY (No Chip)
             LocalConfig.MiniSelectedPinnedPromptId = "";
             textToInsert = prompt.Content ?? "";
        }
        else
        {
             // Combo Mode: Show Chip + Preserve Text
             LocalConfig.MiniSelectedPinnedPromptId = prompt.Id;
        }

        // 3. Insert prompt content/chip to mini window input
        WeakReferenceMessenger.Default.Send(new InsertTextToMiniInputMessage(textToInsert));
    }

    private void UpdateGlobalPromptList(string filterText = "")
    {
        var groups = new List<PromptGroup>();
        string filter = NormalizeSymbols(filterText?.Trim() ?? "");

        // 1. Default (Uncategorized or Root) - only if filter matches
        var rootPrompts = Files.Where(f => string.IsNullOrEmpty(f.FolderId)).ToList();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            rootPrompts = rootPrompts
                .Where(p => 
                    NormalizeSymbols(p.Title).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeSymbols(p.Content ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }

        if (rootPrompts.Any())
        {
            groups.Add(new PromptGroup { FolderName = "默认", Prompts = rootPrompts });
        }

        // 2. Folders
        foreach (var folder in Folders)
        {
            var folderPrompts = Files.Where(f => f.FolderId == folder.Id).ToList();
            
            if (!string.IsNullOrWhiteSpace(filter))
            {
                folderPrompts = folderPrompts
                    .Where(p => 
                        NormalizeSymbols(p.Title).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        NormalizeSymbols(p.Content ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            if (folderPrompts.Any())
            {
                groups.Add(new PromptGroup { FolderName = folder.Name, Prompts = folderPrompts });
            }
        }

        GlobalPromptList = new ObservableCollection<PromptGroup>(groups);
    }
}
