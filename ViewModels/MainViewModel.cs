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

namespace PromptMasterv5.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDataService _localDataService;
    private readonly GlobalKeyService _keyService;
    private readonly IAiService _aiService;
    private readonly FabricService _fabricService;
    private readonly BaiduService _baiduService;
    private readonly GoogleService _googleService;

    private DispatcherTimer _timer;
    private DispatcherTimer _localBackupTimer;
    private bool _previousFullMode = true;
    private IntPtr _previousWindowHandle = IntPtr.Zero;
    private bool _isSimulatingKeys;

    public SidebarViewModel SidebarVM { get; }
    public ChatViewModel ChatVM { get; }
    public SettingsViewModel SettingsVM { get; }

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

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        OnPropertyChanged(nameof(HasVariables));
        OnPropertyChanged(nameof(Variables));
        IsEditMode = false; // Always default to preview mode when selecting a file
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
        FabricService fabricService,
        BaiduService baiduService,
        GoogleService googleService,
        ChatViewModel chatVM,
        SidebarViewModel sidebarVM)
    {
        _aiService = aiService;
        _dataService = dataService;
        _localDataService = localDataService;
        _keyService = keyService;
        _fabricService = fabricService;
        _baiduService = baiduService;
        _googleService = googleService;

        SidebarVM = sidebarVM;
        ChatVM = chatVM;
        SettingsVM = settingsVM;

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

        _localBackupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _localBackupTimer.Tick += async (_, __) =>
        {
            _localBackupTimer.Stop();
            await PerformLocalBackup();
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
        if (Config.EnableDoubleCtrl) try { _keyService.Start(); } catch { }

        // 委托给 SettingsViewModel 处理主题和快捷键
        SettingsVM.SetMainViewModel(this);
        SettingsVM.ApplyTheme();
        UpdateWindowHotkeys(); // 使用 MainViewModel 的版本，因为它注册窗口切换快捷键

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
        SidebarVM.Files = Files;

        SidebarVM.Folders = new ObservableCollection<FolderItem>(data.Folders ?? new());
        if (SidebarVM.Folders.Count == 0)
        {
            var defaultFolder = new FolderItem { Name = "默认" };
            SidebarVM.Folders.Add(defaultFolder);
            SidebarVM.SelectedFolder = defaultFolder;
        }
        else
        {
            SidebarVM.SelectedFolder ??= SidebarVM.Folders.FirstOrDefault();
        }

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

        SyncMiniPinnedPrompts();
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
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        var targetFolder = SidebarVM.SelectedFolder;
        if (targetFolder == null)
        {
            targetFolder = new FolderItem { Name = "导入" };
            SidebarVM.Folders.Add(targetFolder);
            SidebarVM.SelectedFolder = targetFolder;
        }

        foreach (var filePath in dialog.FileNames)
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
        Clipboard.SetText(text);
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
            win.Topmost = true;
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

    private void HandleMiniInputTextChangedFromChat(string value)
    {
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

            ChatVM.UpdateSearchPopup(value.Trim());
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

        // 优先使用外部工具设置的快捷键，如果为空则使用 LocalConfig
        string ocrKey = !string.IsNullOrWhiteSpace(Config.OcrHotkey) ? Config.OcrHotkey : LocalConfig.OcrHotkey;
        string translateKey = !string.IsNullOrWhiteSpace(Config.ScreenshotTranslateHotkey) ? Config.ScreenshotTranslateHotkey : LocalConfig.TranslateHotkey;

        RegisterWindowHotkey("TriggerOcrHotkey", ocrKey, () => TriggerOcrCommand.Execute(null));
        RegisterWindowHotkey("TriggerTranslateHotkey", translateKey, () => TriggerTranslateCommand.Execute(null));
    }

    [SupportedOSPlatform("windows")]
    public void UpdateExternalToolsHotkeys()
    {
        // Remove old hotkeys
        try { HotkeyManager.Current.Remove("ScreenshotTranslate"); } catch { }
        try { HotkeyManager.Current.Remove("OcrOnly"); } catch { }

        // Register new hotkeys from external tools settings
        RegisterWindowHotkey("ScreenshotTranslate", Config.ScreenshotTranslateHotkey, () => TriggerTranslateCommand.Execute(null));
        RegisterWindowHotkey("SelectedTextTranslate", Config.SelectedTextTranslateHotkey, () => TriggerSelectedTextTranslateCommand.Execute(null));
        RegisterWindowHotkey("OcrOnly", Config.OcrHotkey, () => TriggerOcrCommand.Execute(null));

        ConfigService.Save(Config);
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
            win.Topmost = true;
            return;
        }

        if (IsFullMode != targetFull)
        {
            IsFullMode = targetFull;
            win.Activate();
            win.Topmost = true;
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
        _localBackupTimer.Stop();
        _localBackupTimer.Start();
    }

    public async Task PerformLocalBackup()
    {
        try
        {
            await _localDataService.SaveAsync(Folders, Files);
        }
        catch { }
    }

    private void UpdateTimeDisplay()
    {
        var now = DateTime.Now;
        SyncTimeDisplay = now.ToString("HH:mm:ss");
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

        if (!IsFullMode) ChatVM.MiniInputText = "";
        else AdditionalInput = "";

        if (wasMiniMode && window != null)
        {
            window.Show();
            window.Activate();
            window.Topmost = true;
            if (window is MainWindow mainWin) mainWin.MiniInputBox.Focus();
        }
    }

    // ==========================================================
    //  🚀 核心修复区域：百度翻译与OCR集成
    // ==========================================================

    /// <summary>
    /// 统一配置百度服务，防止参数传错或混淆
    /// </summary>
    private bool TryGetTranslateProfile(out ApiProfile transProfile)
    {
        // Check if AI translation is enabled
        if (Config.EnableAiTranslation)
        {
            // AI translation doesn't use ApiProfile, return a dummy one
            transProfile = new ApiProfile();
            return true;
        }

        // Check if Baidu is enabled
        if (!Config.EnableBaidu)
        {
            MessageBox.Show("百度翻译未启用。请在设置 → 外部工具 → 主界面中启用百度供应商或 AI 翻译。", "翻译未启用");
            transProfile = null!;
            return false;
        }

        // Try to find Baidu Translation profile
        var t = Config.ApiProfiles.FirstOrDefault(p => 
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);
        
        if (t == null)
        {
            MessageBox.Show("请先在设置 → 外部工具 → 百度标签页中配置翻译 API 密钥。", "翻译未配置");
            transProfile = null!;
            return false;
        }

        transProfile = t;
        return true;
    }

    private bool TryGetOcrProfile(out ApiProfile ocrProfile)
    {
        // Check if Baidu is enabled
        if (!Config.EnableBaidu)
        {
            MessageBox.Show("百度 OCR 未启用。请在设置 → 外部工具 → 主界面中启用百度供应商。", "OCR 未启用");
            ocrProfile = null!;
            return false;
        }

        // Try to find Baidu OCR profile
        var o = Config.ApiProfiles.FirstOrDefault(p => 
            p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);
        
        if (o == null)
        {
            MessageBox.Show("请先在设置 → 外部工具 → 百度标签页中配置 OCR API 密钥。", "OCR 未配置");
            ocrProfile = null!;
            return false;
        }

        ocrProfile = o;
        return true;
    }

    private async Task<string> PerformTranslation(string text)
    {
        if (Config.EnableAiTranslation)
        {
            return await TranslateWithAiAsync(text);
        }
        else if (Config.EnableGoogle)
        {
            var profile = Config.ApiProfiles.FirstOrDefault(p => p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1))
            {
                 MessageBox.Show("请先在外部工具设置中配置 Google 翻译 API Key", "配置错误");
                 return "";
            }
            return await _googleService.TranslateAsync(text, profile);
        }
        else
        {
             if (!TryGetTranslateProfile(out var transProfile)) 
             {
                 MessageBox.Show("请先配置百度翻译或启用其他翻译服务", "未配置翻译服务");
                 return "";
             }
             return await _baiduService.TranslateAsync(text, transProfile);
        }
    }

    [RelayCommand]
    private async Task TriggerSelectedTextTranslate()
    {
        try
        {
            // 1. 清空剪贴板
            try { System.Windows.Clipboard.Clear(); } catch { }

            // 2. 模拟 Ctrl+C
            // 确保当前窗口是前台窗口（通常按下快捷键时是的，但为了保险）
            // IntPtr foreground = NativeMethods.GetForegroundWindow();
            // NativeMethods.SetForegroundWindow(foreground);

            _isSimulatingKeys = true;
            try 
            {
                NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
                NativeMethods.keybd_event(NativeMethods.VK_C, 0, 0, 0);
                NativeMethods.keybd_event(NativeMethods.VK_C, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
                NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
                
                // 3. 循环检查剪贴板
                string text = "";
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(50);
                    try 
                    {
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            text = System.Windows.Clipboard.GetText();
                            if (!string.IsNullOrWhiteSpace(text)) break;
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    // 如果获取失败，尝试通过弹窗提示用户（可能是权限问题）
                    // MessageBox.Show("无法获取选中文本。如果目标软件以管理员身份运行，请尝试以管理员身份运行本程序。", "获取文本失败");
                    return;
                }

                var translated = await PerformTranslation(text);

                if (string.IsNullOrWhiteSpace(translated)) return;

                if (Config.AutoCopyTranslationResult)
                {
                    try { await Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(translated)); } catch { }
                }

                // 4. 显示结果
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                        var popup = new Views.TranslationPopup(translated);
                        popup.Show();
                        popup.Activate();
                });
            }
            finally
            {
                // 延迟一小段时间重置标志，确保所有钩子消息都处理完毕
                await Task.Delay(200);
                _isSimulatingKeys = false;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"划词翻译出错: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private async Task TriggerOcr()
    {
        if (!TryGetOcrProfile(out var ocrProfile)) return;

        var capture = new Views.CaptureWindow();
        if (capture.ShowDialog() != true || capture.CapturedImageBytes == null) return;

        // 执行 OCR
        var result = await _baiduService.OcrAsync(capture.CapturedImageBytes, ocrProfile);

        if (!string.IsNullOrWhiteSpace(result))
        {
            if (result.StartsWith("OCR 错误") || result.StartsWith("错误"))
            {
                MessageBox.Show(result, "OCR 失败");
            }
            else
            {
                Clipboard.SetText(result);
            }
        }
    }

    [RelayCommand]
    private async Task TriggerTranslate()
    {
        // if (!TryGetTranslateProfile(out var transProfile)) return; // 移除此检查，因为可能使用 Google 或 AI
        if (!TryGetOcrProfile(out var ocrProfile)) return;

        var capture = new Views.CaptureWindow();
        if (capture.ShowDialog() != true || capture.CapturedImageBytes == null) return;

        // 1. 先进行 OCR 识别文字
        var text = await _baiduService.OcrAsync(capture.CapturedImageBytes, ocrProfile);
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith("OCR 错误") || text.StartsWith("错误"))
        {
            MessageBox.Show(text, "文字识别失败");
            return;
        }

        // 2. 统一翻译逻辑
        var translated = await PerformTranslation(text);

        if (Config.AutoCopyTranslationResult && !string.IsNullOrWhiteSpace(translated))
        {
            try { Clipboard.SetText(translated); } catch { }
        }

        // 3. 显示结果
        var popup = new Views.TranslationPopup(translated);
        popup.Show();
    }

    private async Task<string> TranslateWithAiAsync(string text)
    {
        try
        {
            // 获取选中的翻译提示词
            string systemPrompt = "You are a professional translator. Translate the following text to Chinese.";
            
            if (!string.IsNullOrWhiteSpace(Config.AiTranslationPromptId))
            {
                var promptFile = Files.FirstOrDefault(f => f.Id == Config.AiTranslationPromptId);
                if (promptFile != null && !string.IsNullOrWhiteSpace(promptFile.Content))
                {
                    systemPrompt = promptFile.Content;
                }
            }

            // 调用 AI 服务
            var result = await _aiService.ChatAsync(text, Config.AiTranslateApiKey, Config.AiTranslateBaseUrl, Config.AiTranslateModel, systemPrompt);

            return result ?? "AI 翻译失败：未返回结果";
        }
        catch (Exception ex)
        {
            return $"AI 翻译异常: {ex.Message}";
        }
    }
}
