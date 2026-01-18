using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using NHotkey;
using NHotkey.Wpf;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
using PromptMasterv5.Core.Interfaces;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.IO;

using InputMode = PromptMasterv5.Core.Models.InputMode;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;

namespace PromptMasterv5.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDataService _dataService; // 云端服务 (WebDAV)

        // ★★★ 方案A：新增本地热备份服务 ★★★
        private readonly IDataService _localDataService;

        private readonly GlobalKeyService _keyService;
        private readonly BrowserAutomationService _browserService;
        private readonly IAiService _aiService;
        private readonly FabricService _fabricService;
        private readonly BaiduService _baiduService;

        private bool _isCreatingFile = false;
        private DispatcherTimer _timer;

        // ★★★ 方案A：新增本地热备份防抖定时器 ★★★
        private DispatcherTimer _localBackupTimer;

        private DateTime _lastSyncTime = DateTime.Now;
        private IntPtr _previousWindowHandle = IntPtr.Zero;
        private bool _previousFullMode = true;

        [ObservableProperty] private AppConfig config;
        [ObservableProperty] private LocalSettings localConfig = new LocalSettings();
        [ObservableProperty] private bool isFullMode = true;
        public SidebarViewModel SidebarVM { get; }
        public ChatViewModel ChatVM { get; }
        public string MiniInputText { get => ChatVM.MiniInputText; set => ChatVM.MiniInputText = value; }
        public bool IsSearchPopupOpen { get => ChatVM.IsSearchPopupOpen; set => ChatVM.IsSearchPopupOpen = value; }
        public ObservableCollection<PromptItem> SearchResults => ChatVM.SearchResults;
        public PromptItem? SelectedSearchItem { get => ChatVM.SelectedSearchItem; set => ChatVM.SelectedSearchItem = value; }
        [ObservableProperty] private bool isMiniVarsExpanded = false;

        [ObservableProperty] private bool isSettingsOpen = false;
        [ObservableProperty] private int selectedSettingsTab = 0;
        [ObservableProperty] private string syncTimeDisplay = "Now";
        [ObservableProperty] private ICollectionView? filesView;
        public IDropTarget FolderDropHandler => SidebarVM.FolderDropHandler;
        [ObservableProperty] private bool isNavigationVisible = true;
        public ObservableCollection<FolderItem> Folders => SidebarVM.Folders;
        public FolderItem? SelectedFolder { get => SidebarVM.SelectedFolder; set => SidebarVM.SelectedFolder = value; }

        [ObservableProperty] private ObservableCollection<PromptItem> files = new();
        [ObservableProperty] private PromptItem? selectedFile;
        [ObservableProperty] private bool isEditMode;
        [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
        [ObservableProperty] private bool hasVariables;
        [ObservableProperty] private string additionalInput = "";

        public bool IsAiProcessing { get => ChatVM.IsAiProcessing; set => ChatVM.IsAiProcessing = value; }
        [ObservableProperty] private bool isAiResultDisplayed = false;

        [ObservableProperty] private bool isDirty = false; 

        public ObservableCollection<PromptItem> MiniPinnedPrompts => ChatVM.MiniPinnedPrompts;
        public IDropTarget MiniPinnedPromptDropHandler { get; private set; }

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
            SidebarVM = sidebarVM;
            ChatVM = chatVM;

            // 1. 初始化配置
            Config = ConfigService.Load();
            LocalConfig = LocalConfigService.Load();
            ApplyTheme(LocalConfig.Theme);
            UpdateWindowHotkeys();

            // 2. 初始化所有服务
            _dataService = dataService;
            _localDataService = localDataService;
            _aiService = aiService;
            _fabricService = fabricService;
            _baiduService = baiduService;

            // ★★★ 方案A：初始化本地热备份定时器 (2秒防抖) ★★★
            _localBackupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _localBackupTimer.Tick += async (s, e) =>
            {
                _localBackupTimer.Stop();
                await PerformLocalBackup();
            };

            _browserService = browserService;
            _browserService.OnTargetSiteMatched += BrowserService_OnTargetSiteMatched;
            _browserService.Start();

            SidebarVM.Files = Files;
            SidebarVM.GetSelectedFile = () => SelectedFile;
            SidebarVM.SelectFile = f => SelectedFile = f;
            SidebarVM.SetEditMode = v => IsEditMode = v;
            SidebarVM.RequestSave = RequestSave;
            SidebarVM.MoveFileToFolder = MoveFileToFolder;

            SidebarVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SidebarViewModel.Folders)) OnPropertyChanged(nameof(Folders));
                if (e.PropertyName == nameof(SidebarViewModel.SelectedFolder))
                {
                    OnPropertyChanged(nameof(SelectedFolder));
                    FilesView?.Refresh();
                    SelectedFile = null;
                }
            };

            ChatVM.ConfigProvider = () => Config;
            ChatVM.LocalConfigProvider = () => LocalConfig;
            ChatVM.FilesProvider = () => Files;
            ChatVM.GetIsAiResultDisplayed = () => IsAiResultDisplayed;
            ChatVM.SetIsAiResultDisplayed = v => IsAiResultDisplayed = v;
            ChatVM.MiniInputTextChangedExternalHandler = HandleMiniInputTextChangedFromChat;

            ChatVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatViewModel.MiniInputText)) OnPropertyChanged(nameof(MiniInputText));
                if (e.PropertyName == nameof(ChatViewModel.IsSearchPopupOpen)) OnPropertyChanged(nameof(IsSearchPopupOpen));
                if (e.PropertyName == nameof(ChatViewModel.SelectedSearchItem)) OnPropertyChanged(nameof(SelectedSearchItem));
                if (e.PropertyName == nameof(ChatViewModel.IsAiProcessing)) OnPropertyChanged(nameof(IsAiProcessing));
            };

            // 3. 初始化拖拽处理器
            MiniPinnedPromptDropHandler = new PinnedPromptDropHandler(this);

            // 4. 初始化定时器
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateTimeDisplay();
            _timer.Start();

            // 5. 初始化按键监听服务
            _keyService = keyService;
            _keyService.OnDoubleCtrlDetected += (s, e) => Application.Current.Dispatcher.Invoke(() => ToggleMainWindow());

            _keyService.OnDoubleSemiColonDetected += (s, e) => Application.Current.Dispatcher.Invoke(async () =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow == null) return;

                if (!IsFullMode && mainWindow.Visibility == Visibility.Visible && mainWindow.IsActive)
                {
                    MiniInputText = "";
                }
                else
                {
                    if (mainWindow.Visibility != Visibility.Visible)
                    {
                        ToggleMainWindow();
                    }
                    MiniInputText = "";
                }

                await Task.Delay(50);

                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Topmost = true;

                var interopHelper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                NativeMethods.SetForegroundWindow(interopHelper.Handle);

                mainWindow.MiniInputBox.Focus();
                Keyboard.Focus(mainWindow.MiniInputBox);

                await Task.Delay(20);

                if (!string.IsNullOrEmpty(MiniInputText))
                {
                    string cleaned = MiniInputText.TrimStart(';', '；');
                    if (cleaned != MiniInputText)
                    {
                        MiniInputText = cleaned;
                    }
                }
                if (mainWindow.MiniInputBox.Document != null)
                {
                    mainWindow.MiniInputBox.CaretPosition = mainWindow.MiniInputBox.Document.ContentEnd;
                    mainWindow.MiniInputBox.ScrollToEnd();
                }
            });

            _keyService.AlwaysOnTopSequence = LocalConfig.MiniAlwaysOnTopHotkeyPrefix;
            _keyService.OnAlwaysOnTopSequenceDetected += (s, e) => Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow == null) return;

                if (IsFullMode) IsFullMode = false;
                LocalConfig.IsMiniTopmostLocked = true;

                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Topmost = true;

                var interopHelper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                NativeMethods.SetForegroundWindow(interopHelper.Handle);

                mainWindow.MiniInputBox.Focus();
                Keyboard.Focus(mainWindow.MiniInputBox);
            });

            if (Config.EnableDoubleCtrl) try { _keyService.Start(); } catch { }

            _ = InitializeAsync();
        }

        public void AddMiniPinnedPromptFromCandidate()
        {
            ChatVM.AddMiniPinnedPromptFromCandidate(LocalConfig.MiniPinnedPromptIds, LocalConfig.MiniPinnedPromptCandidateId ?? "");
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
        }

        public void ReorderMiniPinnedPrompts(int oldIndex, int newIndex)
        {
            ChatVM.ReorderMiniPinnedPrompts(LocalConfig.MiniPinnedPromptIds, oldIndex, newIndex);
        }

        private void SyncMiniPinnedPrompts()
        {
            ChatVM.SyncMiniPinnedPrompts(LocalConfig.MiniPinnedPromptIds, LocalConfig.MiniSelectedPinnedPromptId);
            if (!string.IsNullOrWhiteSpace(LocalConfig.MiniSelectedPinnedPromptId) && !LocalConfig.MiniPinnedPromptIds.Contains(LocalConfig.MiniSelectedPinnedPromptId))
            {
                LocalConfig.MiniSelectedPinnedPromptId = "";
            }
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            LocalConfig.Theme = LocalConfig.Theme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark;
            ApplyTheme(LocalConfig.Theme);
            LocalConfigService.Save(LocalConfig);
        }

        [RelayCommand]
        private void EnableMiniAiMode()
        {
            LocalConfig.MiniAiOnlyChatEnabled = true;
            if (string.IsNullOrWhiteSpace(LocalConfig.MiniPatternPrefix))
            {
                LocalConfig.MiniPatternPrefix = "ai";
            }
            LocalConfigService.Save(LocalConfig);
        }

        [RelayCommand]
        private void EnableMiniTestMode()
        {
            LocalConfig.MiniAiOnlyChatEnabled = false;
            LocalConfigService.Save(LocalConfig);
        }

        private void ApplyTheme(ThemeType theme)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            static System.Windows.Media.Color ParseColor(string value) =>
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);

            static void SetBrush(ResourceDictionary resources, string key, string color)
            {
                resources[key] = new System.Windows.Media.SolidColorBrush(ParseColor(color));
            }

            if (theme == ThemeType.Dark)
            {
                SetBrush(resources, "ShellBackground", "#2E3033");
                SetBrush(resources, "Block1Background", "#2E3033");
                SetBrush(resources, "Block2Background", "#2E3033");
                SetBrush(resources, "Block3Background", "#363B40");
                SetBrush(resources, "Block4Background", "#363B40");

                SetBrush(resources, "PrimaryTextBrush", "#B8BFC6");
                SetBrush(resources, "SecondaryTextBrush", "#B8BFC6");
                SetBrush(resources, "PlaceholderTextBrush", "#B8BFC6");

                SetBrush(resources, "DividerBrush", "#4A4A55");

                SetBrush(resources, "ActionIconBrush", "#B0B4B8");
                SetBrush(resources, "ActionIconHoverBrush", "#FFFFFF");
                SetBrush(resources, "HeaderIconBrush", "#B0B4B8");
                SetBrush(resources, "HeaderIconHoverBrush", "#FFFFFF");

                SetBrush(resources, "InputFocusBackgroundBrush", "#2E3033");
                SetBrush(resources, "ListItemHoverBackgroundBrush", "#3A3F45");
                SetBrush(resources, "ListItemSelectedBackgroundBrush", "#444A52");

                SetBrush(resources, "AppBackground", "#2E3033");
                SetBrush(resources, "SidebarBackground", "#363B40");
                SetBrush(resources, "CardBackground", "#2E3033");
                SetBrush(resources, "TextPrimary", "#B8BFC6");
                SetBrush(resources, "TextSecondary", "#B8BFC6");
                SetBrush(resources, "DividerColor", "#4A4A55");
                SetBrush(resources, "HintBrush", "#B8BFC6");

                // Markdown theme colors for dark theme
                SetBrush(resources, "GithubTextBrush", "#B8BFC6");
                SetBrush(resources, "GithubLinkBrush", "#58A6FF");
                SetBrush(resources, "GithubBorderBrush", "#30363D");
                SetBrush(resources, "GithubCodeBgBrush", "#1E1E1E");
                SetBrush(resources, "GithubQuoteBorderBrush", "#52586F");
            }

            if (theme == ThemeType.Dark)
            {
                SetBrush(resources, "ShellBackground", "#2E3033");
                SetBrush(resources, "Block1Background", "#2E3033");
                SetBrush(resources, "Block2Background", "#2E3033");
                SetBrush(resources, "Block3Background", "#363B40");
                SetBrush(resources, "Block4Background", "#363B40");

                SetBrush(resources, "PrimaryTextBrush", "#B8BFC6");
                SetBrush(resources, "SecondaryTextBrush", "#B0B4B8");
                SetBrush(resources, "PlaceholderTextBrush", "#8A8F96");

                SetBrush(resources, "DividerBrush", "#4A4F55");

                SetBrush(resources, "ActionIconBrush", "#B0B4B8");
                SetBrush(resources, "ActionIconHoverBrush", "#FFFFFF");
                SetBrush(resources, "HeaderIconBrush", "#B0B4B8");
                SetBrush(resources, "HeaderIconHoverBrush", "#FFFFFF");

                SetBrush(resources, "InputFocusBackgroundBrush", "#2E3033");
                SetBrush(resources, "ListItemHoverBackgroundBrush", "#3A3F45");
                SetBrush(resources, "ListItemSelectedBackgroundBrush", "#444A52");

                SetBrush(resources, "AppBackground", "#2E3033");
                SetBrush(resources, "SidebarBackground", "#363B40");
                SetBrush(resources, "CardBackground", "#2E3033");
                SetBrush(resources, "TextPrimary", "#E6E8EA");
                SetBrush(resources, "TextSecondary", "#B0B4B8");
                SetBrush(resources, "DividerColor", "#4A4F55");
                SetBrush(resources, "HintBrush", "#8A8F96");
            }
            else
            {
                SetBrush(resources, "ShellBackground", "#FAFAFA");
                SetBrush(resources, "Block1Background", "#F1F1EF");
                SetBrush(resources, "Block2Background", "#F1F1EF");
                SetBrush(resources, "Block3Background", "#FAFAFA");
                SetBrush(resources, "Block4Background", "#FAFAFA");

                SetBrush(resources, "PrimaryTextBrush", "#333333");
                SetBrush(resources, "SecondaryTextBrush", "#666666");
                SetBrush(resources, "PlaceholderTextBrush", "#999999");

                SetBrush(resources, "DividerBrush", "#E0E0E0");

                SetBrush(resources, "ActionIconBrush", "#898888");
                SetBrush(resources, "ActionIconHoverBrush", "#000000");
                SetBrush(resources, "HeaderIconBrush", "#666666");
                SetBrush(resources, "HeaderIconHoverBrush", "#333333");

                SetBrush(resources, "InputFocusBackgroundBrush", "#FFFFFF");
                SetBrush(resources, "ListItemHoverBackgroundBrush", "#EAEAEA");
                SetBrush(resources, "ListItemSelectedBackgroundBrush", "#E0E0E0");

                SetBrush(resources, "AppBackground", "#F1F1EF");
                SetBrush(resources, "SidebarBackground", "#F7F7F7");
                SetBrush(resources, "CardBackground", "#FFFFFF");
                SetBrush(resources, "TextPrimary", "#333333");
                SetBrush(resources, "TextSecondary", "#666666");
                SetBrush(resources, "DividerColor", "#E5E5E5");
                SetBrush(resources, "HintBrush", "#999999");
            }
        }

        private void HandleMiniInputTextChangedFromChat(string value)
        {
            if (IsAiResultDisplayed && !string.IsNullOrWhiteSpace(value))
            {
                IsAiResultDisplayed = false;
            }

            if (!LocalConfig.MiniAiOnlyChatEnabled)
            {
                IsSearchPopupOpen = false;
                Variables.Clear();
                HasVariables = false;
                IsMiniVarsExpanded = false;
                return;
            }

            var prefix = LocalConfig.MiniPatternPrefix ?? "";
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                var normalizedValue = NormalizeSymbols(value);
                var normalizedPrefix = NormalizeSymbols(prefix);
                if (normalizedValue.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                {
                    IsSearchPopupOpen = false;
                    Variables.Clear();
                    HasVariables = false;
                    IsMiniVarsExpanded = false;
                    return;
                }
            }

            IsSearchPopupOpen = false;
            ParseVariablesRealTime(value);
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
                    mainWindow.Activate();
                    mainWindow.Focus();
                    mainWindow.Topmost = true;
                    if (mainWindow is MainWindow win) win.MiniInputBox.Focus();
                }
            });
        }

        [SupportedOSPlatform("windows")]
        public void UpdateWindowHotkeys()
        {
            try { HotkeyManager.Current.Remove("ToggleWindow"); } catch { }
            try { HotkeyManager.Current.Remove("ToggleWindowSingle"); } catch { }

            RegisterWindowHotkey("ToggleFullWindowHotkey", Config.FullWindowHotkey, () => ToggleWindowToMode(true));
RegisterWindowHotkey("ToggleMiniWindowHotkey", Config.MiniWindowHotkey, () => ToggleWindowToMode(false)); 

// 注册 OCR 与 翻译 热键 
        RegisterWindowHotkey("TriggerOcrHotkey", LocalConfig.OcrHotkey, () => TriggerOcrCommand.Execute(null)); 
        RegisterWindowHotkey("TriggerTranslateHotkey", LocalConfig.TranslateHotkey, () => TriggerTranslateCommand.Execute(null)); 
    } 

    [RelayCommand] 
    private async Task TriggerOcr() 
    { 
        // 1. 检查配置 (使用独立的 OCR 配置)
        var profile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.OcrProfileId);
        if (profile == null)
        {
            // 尝试默认使用第一个
            profile = Config.ApiProfiles.FirstOrDefault();
            if (profile == null)
            {
                MessageBox.Show("请先在设置 -> 外部工具中添加并绑定 OCR 配置。", "OCR 未配置");
                return;
            }
        }

        // 2. 截图 
        var capture = new Views.CaptureWindow(); 
        if (capture.ShowDialog() != true || capture.CapturedImageBytes == null) return; 

        // 3. 识别 (更改鼠标状态为等待) 
        var originalCursor = System.Windows.Input.Mouse.OverrideCursor; 
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait; 

        try 
        { 
            string result = await _baiduService.OcrAsync(profile.Key1, profile.Key2, capture.CapturedImageBytes); 
            if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("错误") && !result.StartsWith("异常")) 
            { 
                Clipboard.SetText(result); 
                // 简单的视觉反馈（如果当前在全屏模式，可以通过状态栏显示，这里简单弹个Toast或不打扰） 
                // 按照需求：写入剪贴板后恢复鼠标即可 
            } 
            else 
            { 
                MessageBox.Show(result, "识别失败"); 
            } 
        } 
        finally 
        { 
            System.Windows.Input.Mouse.OverrideCursor = originalCursor; 
        } 
    } 

    [RelayCommand]
    private async Task TriggerTranslate()
    {
        // 1. 获取 OCR 配置 (专款专用：用于文字识别)
        var ocrProfile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.OcrProfileId);
        if (ocrProfile == null) ocrProfile = Config.ApiProfiles.FirstOrDefault();

        // 2. 获取 翻译 配置 (专款专用：用于文本翻译)
        var transProfile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.TranslateProfileId);
        if (transProfile == null) transProfile = Config.ApiProfiles.FirstOrDefault();

        // 3. 校验配置完整性
        if (ocrProfile == null || transProfile == null)
        {
            MessageBox.Show("请先在设置 -> 外部工具中添加并绑定 OCR 和 翻译 的接口信息。", "配置缺失");
            return;
        }

        // 4. 截图
        var capture = new Views.CaptureWindow();
        if (capture.ShowDialog() != true || capture.CapturedImageBytes == null) return;

        // 5. 显示结果弹窗 (Loading)
        var popup = new Views.TranslationPopup("正在识别并翻译...");
        popup.Show();

        try
        {
            // 6. 调用 OCR (使用 ocrProfile)
            string ocrText = await _baiduService.OcrAsync(ocrProfile.Key1, ocrProfile.Key2, capture.CapturedImageBytes);

            if (string.IsNullOrWhiteSpace(ocrText) || ocrText.StartsWith("错误") || ocrText.StartsWith("异常"))
            {
                popup.UpdateText($"识别失败: {ocrText}");
                return;
            }

            popup.UpdateText("正在翻译...");

            // 7. 调用 翻译 (使用 transProfile)
            // 修正点：这里传入的是 transProfile 的 Key1 (即 AppID) 和 Key2 (即 密钥)
            string transResult = await _baiduService.TranslateAsync(transProfile.Key1, transProfile.Key2, ocrText);

            popup.UpdateText(transResult);
        }
        catch (Exception ex)
        {
            popup.UpdateText($"错误: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddApiProfile()
    {
        var p = new ApiProfile();
        Config.ApiProfiles.Add(p);
        // 如果当前没有选中任何配置，默认选中这个
        if (string.IsNullOrEmpty(Config.OcrProfileId)) Config.OcrProfileId = p.Id;
        if (string.IsNullOrEmpty(Config.TranslateProfileId)) Config.TranslateProfileId = p.Id;
        ConfigService.Save(Config);
    }

    [RelayCommand]
    private void DeleteApiProfile(ApiProfile p)
    {
        if (p == null) return;
        Config.ApiProfiles.Remove(p);
        // 如果删除的是当前选中的，清空ID
        if (Config.OcrProfileId == p.Id) Config.OcrProfileId = "";
        if (Config.TranslateProfileId == p.Id) Config.TranslateProfileId = "";
        ConfigService.Save(Config);
    }

    public Task<(bool Success, string Message)> TestAiConnectionAsync()
    {
        return _aiService.TestConnectionAsync(Config);
    }

    private void RegisterWindowHotkey(string name, string hotkeyStr, Action action)
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
                    HotkeyManager.Current.AddOrReplace(name, key, modifiers, (s, e) => action());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"热键注册失败({name}): {ex.Message}");
            }
        }

        public Task ExecuteAiQuery()
        {
            return ChatVM.ExecuteAiQuery();
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

        private static string NormalizeSymbols(string s)
        {
            return new string((s ?? "").Select(NormalizeSymbol).ToArray());
        }

        public Task ExecuteMiniAiOrPatternAsync()
        {
            return ChatVM.ExecuteMiniAiOrPatternAsync();
        }

        [RelayCommand]
        private void ConfirmSearchResult()
        {
            ChatVM.ConfirmSearchResultCommand.Execute(null);
        }

        private void ParseVariablesRealTime(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                Variables.Clear(); HasVariables = false; IsMiniVarsExpanded = false; return;
            }
            var matches = Regex.Matches(content, @"\{\{(.*?)\}\}");
            var newVarNames = matches.Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            for (int i = Variables.Count - 1; i >= 0; i--) if (!newVarNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
            foreach (var name in newVarNames) if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
            HasVariables = Variables.Count > 0;
            if (!IsFullMode)
            {
                IsMiniVarsExpanded = HasVariables;
            }
        }

        [RelayCommand] private void ToggleWindowMode() => IsFullMode = !IsFullMode;
        [RelayCommand] private void EnterFullMode() { if (!IsFullMode) IsFullMode = true; }
        [RelayCommand] private void ExitFullMode() { if (IsFullMode) IsFullMode = false; else Application.Current.MainWindow?.Hide(); }

        private string CompileContent()
        {
            string finalContent;
            if (IsFullMode)
            {
                finalContent = SelectedFile?.Content ?? "";
            }
            else
            {
                finalContent = MiniInputText;
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
                foreach (var variable in Variables) finalContent = finalContent.Replace("{{" + variable.Name + "}}", variable.Value ?? "");
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
            else MiniInputText = "";
        }

        public async Task SendByCoordinate()
        {
            string content = CompileContent().TrimEnd();
            await ExecuteSendAsync(content, InputMode.CoordinateClick);
            if (IsFullMode) AdditionalInput = "";
            else MiniInputText = "";
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
                MiniInputText = "";
            }
            else
            {
                AdditionalInput = "";
            }

            if (wasMiniMode && window != null)
            {
                window.Show();
                window.Activate();
                window.Topmost = true;
                if (window is MainWindow mainWin)
                {
                    FocusMiniInputBox(mainWin);
                }
            }
        }

        private static void FocusMiniInputBox(MainWindow window)
        {
            void Focus()
            {
                var box = window.MiniInputBox;
                if (box == null) return;
                box.Focus();
                Keyboard.Focus(box);
            }

            if (window.Dispatcher.CheckAccess())
            {
                Focus();
            }
            else
            {
                window.Dispatcher.Invoke(Focus, DispatcherPriority.Render);
            }
        }

        [RelayCommand]
        private async Task SendFromMini(string modeStr)
        {
            if (modeStr == "Coordinate") await SendByCoordinate();
            else await SendBySmartFocus();
        }

        partial void OnSelectedFileChanged(PromptItem? oldValue, PromptItem? newValue)
        {
            if (oldValue != null) oldValue.PropertyChanged -= SelectedFile_PropertyChanged;
            if (newValue != null)
            {
                newValue.PropertyChanged += SelectedFile_PropertyChanged;
                if (IsFullMode) ParseVariablesRealTime(newValue.Content ?? "");
            }
            else if (IsFullMode) { Variables.Clear(); HasVariables = false; AdditionalInput = ""; }

            if (_isCreatingFile) return;
            IsEditMode = false;
            RequestSave();
        }

        private void SelectedFile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PromptItem.Content))
            {
                if (IsFullMode) ParseVariablesRealTime(SelectedFile?.Content ?? "");

                // ★★★ 方案A：更新最后修改时间，以便版本对比 ★★★
                if (SelectedFile != null) SelectedFile.LastModified = DateTime.Now;
            }

            // 标记为脏数据
            if (!IsDirty) IsDirty = true;

            // ★★★ 方案A：触发本地热备份 (重置防抖计时) ★★★
            _localBackupTimer.Stop();
            _localBackupTimer.Start();
        }

        public void ToggleMainWindow()
        {
            var window = Application.Current.MainWindow;
            if (window == null) return;
            if (window.Visibility == Visibility.Visible)
            {
                _previousFullMode = IsFullMode;
                window.Hide();
            }
            else
            {
                CaptureForegroundWindow();
                if (window.Visibility != Visibility.Visible) IsFullMode = _previousFullMode;
                window.Show(); window.Activate(); window.Focus();
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle);
                window.Topmost = true;

                if (!IsFullMode && window is MainWindow mainWin)
                {
                    FocusMiniInputBox(mainWin);
                }
            }
        }

        public void ToggleWindowToMode(bool targetFullMode)
        {
            var window = Application.Current.MainWindow;
            if (window == null) return;

            if (window.Visibility == Visibility.Visible)
            {
                if (IsFullMode != targetFullMode)
                {
                    IsFullMode = targetFullMode;
                    window.Activate();
                    window.Focus();
                    NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle);
                    window.Topmost = true;

                    if (!IsFullMode && window is MainWindow mainWin)
                    {
                        mainWin.SuppressMiniAutoHide(800);
                        FocusMiniInputBox(mainWin);
                    }
                }
                else
                {
                    if (!window.IsActive)
                    {
                        if (window is MainWindow mainWin)
                        {
                            mainWin.BringToFrontAndEnsureOnScreen();
                            if (!IsFullMode)
                            {
                                FocusMiniInputBox(mainWin);
                            }
                        }
                        else
                        {
                            window.Activate();
                            window.Focus();
                            NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle);
                            window.Topmost = true;
                        }
                        return;
                    }

                    _previousFullMode = IsFullMode;
                    window.Hide();
                }
                return;
            }

            CaptureForegroundWindow();
            IsFullMode = targetFullMode;
            _previousFullMode = targetFullMode;

            window.Show();
            window.Activate();
            window.Focus();
            NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            window.Topmost = true;

            if (!IsFullMode && window is MainWindow miniWin)
            {
                miniWin.SuppressMiniAutoHide(800);
                FocusMiniInputBox(miniWin);
            }
        }

        public void CaptureForegroundWindow()
        {
            var handle = NativeMethods.GetForegroundWindow();
            if (handle != IntPtr.Zero) _previousWindowHandle = handle;
        }

        [RelayCommand]
        private void CreateFolder()
        {
            SidebarVM.CreateFolderCommand.Execute(null);
        }

        [RelayCommand]
        private void CreateFile()
        {
            if (SelectedFolder == null) return;
            _isCreatingFile = true;
            SidebarVM.CreateFileCommand.Execute(null);
            _isCreatingFile = false;
        }
        [RelayCommand] private void DeleteFile(PromptItem? i) { var t = i ?? SelectedFile; if (t != null) { Files.Remove(t); if (SelectedFile == t) SelectedFile = null; RequestSave(); } }
        [RelayCommand]
        private void DeleteFolder(FolderItem? folder)
        {
            SidebarVM.DeleteFolderCommand.Execute(folder);
        }

        [RelayCommand]
        private void ChangeFolderIcon(FolderItem f)
        {
            SidebarVM.ChangeFolderIconCommand.Execute(f);
        }

        [RelayCommand]
        private void RenameFolder(FolderItem f)
        {
            SidebarVM.RenameFolderCommand.Execute(f);
        }
        [RelayCommand]
        private void ChangeFileIcon(PromptItem f)
        {
            if (f == null) return;
            var dialog = new IconInputDialog(f.IconGeometry);
            if (dialog.ShowDialog() == true)
            {
                f.IconGeometry = dialog.ResultGeometry;
                RequestSave();
            }
        }
        [RelayCommand]
        private void ChangeActionIcon(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            var dialog = new IconInputDialog(LocalConfig.ActionIcons.GetValueOrDefault(actionId));
            if (dialog.ShowDialog() == true)
            {
                LocalConfig.ActionIcons[actionId] = dialog.ResultGeometry;
            }
        }
        [RelayCommand] private void OpenSettings() { Config = ConfigService.Load(); LocalConfig = LocalConfigService.Load(); SelectedSettingsTab = 0; IsSettingsOpen = true; }
        [RelayCommand]
        private void SaveSettings()
        {
            string full = (Config.FullWindowHotkey ?? "").Trim();
            string mini = (Config.MiniWindowHotkey ?? "").Trim();
            string fullNorm = full.Replace(" ", "").ToUpperInvariant();
            string miniNorm = mini.Replace(" ", "").ToUpperInvariant();

            if (!string.IsNullOrEmpty(fullNorm) && fullNorm == miniNorm)
            {
                MessageBox.Show("完整窗口热键与迷你窗口热键不能相同。请修改其中一个。", "热键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Config.GlobalHotkey = "";
            Config.SingleHotkey = "";

            ConfigService.Save(Config);
            LocalConfigService.Save(LocalConfig);

            UpdateWindowHotkeys();
            _keyService.AlwaysOnTopSequence = LocalConfig.MiniAlwaysOnTopHotkeyPrefix;
            if (Config.EnableDoubleCtrl) try { _keyService.Start(); } catch { } else _keyService.Stop();

            IsSettingsOpen = false;
        }
        [RelayCommand] private void SelectSettingsTab(string s) { if (int.TryParse(s, out int i)) SelectedSettingsTab = i; }
        public void ReorderFolders(int o, int n) { SidebarVM.ReorderFolders(o, n); }
        public void MoveFileToFolder(PromptItem f, FolderItem t) { if (f == null || t == null || f.FolderId == t.Id) return; f.FolderId = t.Id; FilesView?.Refresh(); if (SelectedFile == f) SelectedFile = null; RequestSave(); }
        [RelayCommand] private void ToggleNavigation() => IsNavigationVisible = !IsNavigationVisible;
        [RelayCommand] private void ToggleEditMode() { IsEditMode = !IsEditMode; if (!IsEditMode) RequestSave(); }
        [RelayCommand] private void CopyCompiledText() { /* ... */ }
        [RelayCommand] private async Task SendDirectPrompt() { await SendFromMini("SmartFocus"); }
        [RelayCommand] private async Task SendCombinedInput() { await SendFromMini("SmartFocus"); }

        [RelayCommand]
        private async Task ManualBackup()
        {
            SyncTimeDisplay = "备份中...";
            try
            {
                ConfigService.Save(Config);
                await _dataService.SaveAsync(Folders, Files);
                _lastSyncTime = DateTime.Now;
                UpdateTimeDisplay();
                IsDirty = false;
            }
            catch (Exception e)
            {
                SyncTimeDisplay = "备份失败";
                MessageBox.Show($"备份失败: {e.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [ObservableProperty]
        private bool _isRestoreConfirmVisible;

        [ObservableProperty]
        private string _restoreStatus = "";

        [ObservableProperty]
        private string _restoreStatusColor = "#666666"; // Default gray

        [RelayCommand]
        private void ShowRestoreConfirm()
        {
            IsRestoreConfirmVisible = true;
            RestoreStatus = ""; // Reset status when opening confirm
        }

        [RelayCommand]
        private void CancelRestoreConfirm()
        {
            IsRestoreConfirmVisible = false;
        }

        [RelayCommand]
        private async Task ManualRestore()
        {
            // 无论结果如何，先关闭确认面板
            IsRestoreConfirmVisible = false;

            // 1. 验证 WebDAV 配置
            if (string.IsNullOrWhiteSpace(Config.WebDavUrl) ||
                string.IsNullOrWhiteSpace(Config.UserName) ||
                string.IsNullOrWhiteSpace(Config.Password))
            {
                RestoreStatus = "❌ 失败: 请先填写完整的 WebDAV 配置";
                RestoreStatusColor = "#E53935";
                return;
            }

            SyncTimeDisplay = "恢复中...";
            RestoreStatus = "🔄 正在恢复...";
            RestoreStatusColor = "#666666";

            try
            {
                // 2. 尝试从云端加载数据
                // 如果配置错误，LoadAsync 应该抛出异常或返回空数据（取决于 DataService 的实现）
                var data = await _dataService.LoadAsync();

                // 3. 检查数据有效性
                // 假设云端至少应该返回一个 PromptData 对象
                // 如果 DataService 在失败时返回空对象而非抛出异常，我们需要在这里判断
                if (data == null)
                {
                    throw new Exception("无法从云端获取数据，请检查网络或配置");
                }

                SelectedFile = null;
                SelectedFolder = null;

                Folders.Clear();
                foreach (var f in data.Folders) Folders.Add(f);

                Files.Clear();
                foreach (var f in data.Files) Files.Add(f);

                if (Folders.Count > 0) SelectedFolder = Folders.FirstOrDefault();

                IsDirty = false;
                _lastSyncTime = DateTime.Now;
                UpdateTimeDisplay();

                RestoreStatus = "✅ 恢复成功";
                RestoreStatusColor = "#43A047";

                // 3秒后清除成功消息
                _ = Task.Delay(3000).ContinueWith(_ => 
                {
                    if (RestoreStatus == "✅ 恢复成功")
                        RestoreStatus = "";
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception e)
            {
                SyncTimeDisplay = "恢复失败";
                RestoreStatus = $"❌ 失败: {e.Message}";
                RestoreStatusColor = "#E53935";
            }
        }

        [RelayCommand]
        private void AddAiModel()
        {
            if (string.IsNullOrWhiteSpace(Config.AiBaseUrl) ||
                string.IsNullOrWhiteSpace(Config.AiApiKey) ||
                string.IsNullOrWhiteSpace(Config.AiModel))
            {
                MessageBox.Show("请先填写完整的模型配置信息", "提示");
                return;
            }

            var existingModel = Config.SavedModels.FirstOrDefault(m => m.ModelName == Config.AiModel);
            if (existingModel != null)
            {
                existingModel.BaseUrl = Config.AiBaseUrl;
                existingModel.ApiKey = Config.AiApiKey;
                Config.ActiveModelId = existingModel.Id;
            }
            else
            {
                var newModel = new AiModelConfig
                {
                    BaseUrl = Config.AiBaseUrl,
                    ApiKey = Config.AiApiKey,
                    ModelName = Config.AiModel
                };
                Config.SavedModels.Add(newModel);
                Config.ActiveModelId = newModel.Id;
            }

            ConfigService.Save(Config);
        }

        [RelayCommand]
        private void ActivateAiModel(AiModelConfig model)
        {
            if (model == null) return;

            Config.AiBaseUrl = model.BaseUrl;
            Config.AiApiKey = model.ApiKey;
            Config.AiModel = model.ModelName;
            Config.ActiveModelId = model.Id;

            ConfigService.Save(Config);
        }

        [RelayCommand]
        private void DeleteAiModel(AiModelConfig model)
        {
            if (model == null) return;

            Config.SavedModels.Remove(model);
            if (Config.ActiveModelId == model.Id)
            {
                Config.ActiveModelId = "";
            }

            ConfigService.Save(Config);
        }

        [RelayCommand]
        private async Task ImportMarkdownFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 Markdown 文件 (支持多选)",
                Filter = "Markdown Files (*.md;*.txt)|*.md;*.txt|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                var targetFolder = SelectedFolder ?? Folders.FirstOrDefault();
                if (targetFolder == null)
                {
                    targetFolder = new FolderItem { Name = "导入的提示词" };
                    Folders.Add(targetFolder);
                    SelectedFolder = targetFolder;
                }

                int count = 0;
                foreach (var filePath in dialog.FileNames)
                {
                    try
                    {
                        string title = Path.GetFileNameWithoutExtension(filePath);
                        string content = await File.ReadAllTextAsync(filePath);

                        var newItem = new PromptItem
                        {
                            Title = title,
                            Content = content,
                            FolderId = targetFolder.Id,
                            LastModified = DateTime.Now
                        };

                        Files.Add(newItem);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"导入文件失败 {filePath}: {ex.Message}");
                    }
                }

                if (count > 0)
                {
                    RequestSave();
                    FilesView?.Refresh();
                    MessageBox.Show($"成功导入 {count} 个文件到文件夹 [{targetFolder.Name}]。", "导入完成");
                }
            }
        }

        private bool FilterFiles(object o) => o is PromptItem f && SelectedFolder != null && f.FolderId == SelectedFolder.Id;

        private void RequestSave()
        {
            if (!IsDirty) IsDirty = true;

            // ★★★ 方案A：数据结构变更时也触发本地热备份 ★★★
            _localBackupTimer.Stop();
            _localBackupTimer.Start();
        }

        // ★★★ 方案A：执行本地热备份的具体实现 ★★★
        private async Task PerformLocalBackup()
        {
            try
            {
                // 静默保存到本地 data.json，用户无感知
                await _localDataService.SaveAsync(Folders, Files);
            }
            catch (Exception ex)
            {
                // 热备份失败不应打断用户，仅记录调试信息
                System.Diagnostics.Debug.WriteLine($"[热备份失败] {ex.Message}");
            }
        }

        private async Task SaveDataAsync() { try { await _dataService.SaveAsync(Folders, Files); _lastSyncTime = DateTime.Now; UpdateTimeDisplay(); } catch { SyncTimeDisplay = "Err"; } }
        private void UpdateTimeDisplay() { var s = DateTime.Now - _lastSyncTime; SyncTimeDisplay = s.TotalSeconds < 60 ? $"{(int)s.TotalSeconds}s" : $"{(int)s.TotalMinutes}m"; }

        // ★★★ 方案A：初始化与智能恢复逻辑 ★★★
        private async Task InitializeAsync()
        {
            try
            {
                // 1. 尝试加载云端数据 (主要数据源)
                var data = await _dataService.LoadAsync();

                // 2. 加载本地热备份数据 (次要数据源，用于灾难恢复)
                var localData = await _localDataService.LoadAsync();

                if (localData.Files.Count > 0 || localData.Folders.Count > 0)
                {
                    var cloudTime = data.Files.Any() ? data.Files.Max(f => f.LastModified) : DateTime.MinValue;
                    var localTime = localData.Files.Any() ? localData.Files.Max(f => f.LastModified) : DateTime.MinValue;

                    // 简单判定：如果本地备份的时间比云端更新，说明上次可能发生了数据丢失或未同步
                    if (localTime > cloudTime)
                    {
                        var result = MessageBox.Show(
                            $"检测到本地热备份数据 (最后修改: {localTime:MM-dd HH:mm}) 比云端数据 (最后修改: {cloudTime:MM-dd HH:mm}) 更新。\n\n这通常是因为上次软件未正常退出或网络同步失败。\n\n是否加载本地备份数据？",
                            "发现未保存的数据",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            data = localData;
                            // 既然加载了本地未上传的数据，标记为 Dirty 以便用户后续手动同步到云端
                            IsDirty = true;
                        }
                    }
                }

                if (data.Folders.Count == 0) data.Folders.Add(new FolderItem { Name = "我的提示词" });
                SidebarVM.Folders.Clear();
                foreach (var folder in data.Folders) SidebarVM.Folders.Add(folder);

                Files.Clear();
                foreach (var file in data.Files) Files.Add(file);

                if (Folders.Count > 0)
                {
                    var fid = Folders.First().Id;
                    foreach (var f in Files) if (string.IsNullOrEmpty(f.FolderId)) f.FolderId = fid;
                }

                var v = CollectionViewSource.GetDefaultView(Files);

                if (v != null) v.Filter = FilterFiles;
                FilesView = v;

                SyncMiniPinnedPrompts();
                Files.CollectionChanged += (s, e) =>
                {
                    RequestSave();
                    SyncMiniPinnedPrompts();
                };
                SelectedFolder = Folders.FirstOrDefault();
                IsFullMode = true;

                // 如果不是从本地恢复的脏数据，则重置为 Clean
                if (!IsDirty) IsDirty = false;
            }
            catch (Exception ex)
            {
                // 捕获所有初始化异常，防止崩溃
                System.Diagnostics.Debug.WriteLine($"初始化数据失败: {ex.Message}");
            }
        }
    }
}
