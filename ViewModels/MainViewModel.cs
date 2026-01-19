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
    private readonly BrowserAutomationService _browserService;
    private readonly IAiService _aiService;
    private readonly FabricService _fabricService;
    private readonly BaiduService _baiduService;

    private DispatcherTimer _timer;
    private DispatcherTimer _localBackupTimer;
    private bool _previousFullMode = true;
    private IntPtr _previousWindowHandle = IntPtr.Zero;

    public SidebarViewModel SidebarVM { get; }
    public ChatViewModel ChatVM { get; }

    public IDropTarget MiniPinnedPromptDropHandler { get; private set; }

    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private LocalSettings localConfig;
    [ObservableProperty] private bool isFullMode = true;
    [ObservableProperty] private bool isMiniVarsExpanded;
    [ObservableProperty] private bool isSettingsOpen;
    [ObservableProperty] private int selectedSettingsTab;
    [ObservableProperty] private string syncTimeDisplay = "Now";
    [ObservableProperty] private ICollectionView? filesView;
    [ObservableProperty] private bool isNavigationVisible = true;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;

    [ObservableProperty] private ObservableCollection<PromptItem> files = new();
    [ObservableProperty] private PromptItem? selectedFile;

    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";

    [ObservableProperty] private bool isDirty;

    public MainViewModel(
        IAiService aiService,
        WebDavDataService dataService,
        FileDataService localDataService,
        GlobalKeyService keyService,
        BrowserAutomationService browserService,
        FabricService fabricService,
        BaiduService baiduService,
        ChatViewModel chatVM,
        SidebarViewModel sidebarVM)
    {
        _aiService = aiService;
        _dataService = dataService;
        _localDataService = localDataService;
        _keyService = keyService;
        _browserService = browserService;
        _fabricService = fabricService;
        _baiduService = baiduService;

        SidebarVM = sidebarVM;
        ChatVM = chatVM;

        Config = ConfigService.Load();
        LocalConfig = LocalConfigService.Load();

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

        _keyService.OnDoubleCtrlDetected += (_, __) => Application.Current.Dispatcher.Invoke(() => ToggleMainWindow());
        _keyService.OnDoubleSemiColonDetected += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            if (!IsFullMode) ChatVM.MiniInputText = "";
        });
        if (Config.EnableDoubleCtrl) try { _keyService.Start(); } catch { }

        _browserService.OnTargetSiteMatched += BrowserService_OnTargetSiteMatched;
        _browserService.Start();

        ApplyTheme(LocalConfig.Theme);
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

    private void UpdateFilesViewFilter()
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

    public string SelectedSettingsTabName => SelectedSettingsTab switch
    {
        0 => "基础",
        1 => "AI",
        2 => "外部工具",
        3 => "发送",
        4 => "迷你",
        5 => "高级",
        _ => "设置"
    };

    partial void OnSelectedSettingsTabChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedSettingsTabName));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try { ConfigService.Save(Config); } catch { }
        try { LocalConfigService.Save(LocalConfig); } catch { }
        UpdateWindowHotkeys();
        IsSettingsOpen = false;
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
    private void ToggleNavigation()
    {
        IsNavigationVisible = !IsNavigationVisible;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        LocalConfig.Theme = LocalConfig.Theme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark;
        ApplyTheme(LocalConfig.Theme);
        LocalConfigService.Save(LocalConfig);
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
            SetBrush(resources, "AppBackground", "#2E3033");
            SetBrush(resources, "SidebarBackground", "#363B40");
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

    private void BrowserService_OnTargetSiteMatched(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return;
            if (!IsFullMode && mainWindow.Visibility == Visibility.Visible)
            {
                CaptureForegroundWindow();
                if (NativeMethods.IsKeyDown(0x01)) return;

                mainWindow.Activate();
                mainWindow.Focus();
                mainWindow.Topmost = true;

                if (mainWindow is MainWindow win)
                {
                    win.MiniInputBox.Focus();
                    if (win.MiniInputBox.Document != null)
                    {
                        win.MiniInputBox.CaretPosition = win.MiniInputBox.Document.ContentEnd;
                        win.MiniInputBox.ScrollToEnd();
                    }
                }
            }
        });
    }

    private void CaptureForegroundWindow()
    {
        try { _previousWindowHandle = NativeMethods.GetForegroundWindow(); } catch { }
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

        RegisterWindowHotkey("TriggerOcrHotkey", LocalConfig.OcrHotkey, () => TriggerOcrCommand.Execute(null));
        RegisterWindowHotkey("TriggerTranslateHotkey", LocalConfig.TranslateHotkey, () => TriggerTranslateCommand.Execute(null));
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

    public Task<(bool Success, string Message)> TestAiConnectionAsync() =>
        _aiService.TestConnectionAsync(Config);

    [RelayCommand]
    private void ActivateAiModel(AiModelConfig? model)
    {
        if (model == null) return;
        Config.ActiveModelId = model.Id;
        ConfigService.Save(Config);
    }

    [RelayCommand]
    private void DeleteAiModel(AiModelConfig? model)
    {
        if (model == null) return;
        var idx = Config.SavedModels.IndexOf(model);
        if (idx >= 0) Config.SavedModels.RemoveAt(idx);
        if (Config.ActiveModelId == model.Id) Config.ActiveModelId = "";
        ConfigService.Save(Config);
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

    private void SyncMiniPinnedPrompts() =>
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

    [RelayCommand]
    private async Task ManualBackup()
    {
        await PerformLocalBackup();
        IsDirty = false;
    }

    private async Task PerformLocalBackup()
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

    [RelayCommand]
    private async Task TriggerOcr()
    {
        var profile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.OcrProfileId) ?? Config.ApiProfiles.FirstOrDefault();
        if (profile == null)
        {
            MessageBox.Show("请先在设置 -> 外部工具中添加并绑定 OCR 配置。", "OCR 未配置");
            return;
        }

        var capture = new Views.CaptureWindow();
        if (capture.ShowDialog() != true || capture.CapturedImageBytes == null) return;

        var result = await _baiduService.OcrAsync(profile.Key1, profile.Key2, capture.CapturedImageBytes);
        if (!string.IsNullOrWhiteSpace(result)) Clipboard.SetText(result);
    }

    [RelayCommand]
    private async Task TriggerTranslate()
    {
        var profile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.TranslateProfileId) ?? Config.ApiProfiles.FirstOrDefault();
        if (profile == null)
        {
            MessageBox.Show("请先在设置 -> 外部工具中添加并绑定 翻译 配置。", "翻译 未配置");
            return;
        }

        var capture = new Views.CaptureWindow();
        if (capture.ShowDialog() != true || capture.CapturedImageBytes == null) return;

        var text = await _baiduService.OcrAsync(profile.Key1, profile.Key2, capture.CapturedImageBytes);
        if (string.IsNullOrWhiteSpace(text)) return;

        var translated = await _baiduService.TranslateAsync(profile.Key1, profile.Key2, text);
        var popup = new Views.TranslationPopup(translated);
        popup.Show();
    }
}
