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
using PromptMasterv5.Models;
using PromptMasterv5.Services;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.IO;

using InputMode = PromptMasterv5.Models.InputMode;
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
        private readonly AiService _aiService;
        private readonly FabricService _fabricService;

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
        [ObservableProperty] private string miniInputText = "";
        [ObservableProperty] private bool isSearchPopupOpen = false;
        [ObservableProperty] private ObservableCollection<PromptItem> searchResults = new();
        [ObservableProperty] private PromptItem? selectedSearchItem;
        [ObservableProperty] private bool isMiniVarsExpanded = false;

        [ObservableProperty] private bool isSettingsOpen = false;
        [ObservableProperty] private int selectedSettingsTab = 0;
        [ObservableProperty] private string syncTimeDisplay = "Now";
        [ObservableProperty] private ICollectionView? filesView;
        public IDropTarget FolderDropHandler { get; private set; }
        [ObservableProperty] private bool isNavigationVisible = true;
        [ObservableProperty] private ObservableCollection<FolderItem> folders = new();

        [ObservableProperty] private FolderItem? selectedFolder;

        [ObservableProperty] private ObservableCollection<PromptItem> files = new();
        [ObservableProperty] private PromptItem? selectedFile;
        [ObservableProperty] private bool isEditMode;
        [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
        [ObservableProperty] private bool hasVariables;
        [ObservableProperty] private string additionalInput = "";

        [ObservableProperty] private bool isAiProcessing = false;
        [ObservableProperty] private bool isAiResultDisplayed = false;

        [ObservableProperty] private bool isDirty = false;

        public MainViewModel()
        {
            // 1. 初始化配置
            Config = ConfigService.Load();
            LocalConfig = LocalConfigService.Load();
            ApplyTheme(LocalConfig.Theme);
            UpdateWindowHotkeys();

            // 2. 初始化所有服务
            _dataService = new WebDavDataService(); // 默认使用 WebDav (云端主存储)

            // ★★★ 方案A：初始化本地服务 (本地副存储) ★★★
            _localDataService = new FileDataService();

            _aiService = new AiService();
            _fabricService = new FabricService();

            // ★★★ 方案A：初始化本地热备份定时器 (2秒防抖) ★★★
            _localBackupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _localBackupTimer.Tick += async (s, e) =>
            {
                _localBackupTimer.Stop();
                await PerformLocalBackup();
            };

            _browserService = new BrowserAutomationService();
            _browserService.OnTargetSiteMatched += BrowserService_OnTargetSiteMatched;
            _browserService.Start();

            // 3. 初始化拖拽处理器
            FolderDropHandler = new FolderDropHandler(this);

            // 4. 初始化定时器
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateTimeDisplay();
            _timer.Start();

            // 5. 初始化按键监听服务
            _keyService = new GlobalKeyService();
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
                mainWindow.MiniInputBox.CaretIndex = mainWindow.MiniInputBox.Text.Length;
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

                SetBrush(resources, "PrimaryTextBrush", "#E6E8EA");
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

        partial void OnMiniInputTextChanged(string value)
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

        public async Task ExecuteAiQuery()
        {
            string inputText = MiniInputText.Trim();
            string query = "";

            bool needPrefix = !LocalConfig.MiniWindowUseAi;

            if (needPrefix)
            {
                if (inputText.StartsWith("ai ", StringComparison.OrdinalIgnoreCase))
                    query = inputText.Substring(3);
                else if (inputText.StartsWith("ai　", StringComparison.OrdinalIgnoreCase))
                    query = inputText.Substring(3);
                else if (inputText.StartsWith("''"))
                    query = inputText.Substring(2);
                else if (inputText.StartsWith("''"))
                    query = inputText.Substring(2);
                else
                    return;
            }
            else
            {
                query = inputText;
            }

            if (string.IsNullOrWhiteSpace(query)) return;

            IsAiProcessing = true;
            try
            {
                string patternContent = await _fabricService.FindBestPatternAndContentAsync(query, _aiService, Config);
                if (!string.IsNullOrEmpty(patternContent))
                {
                    string assembledPrompt = $"{patternContent}\n\n---\n\nUSER INPUT:\n{query}";
                    MiniInputText = assembledPrompt;
                    IsAiResultDisplayed = true;
                }
                else
                {
                    string result = await _aiService.ChatAsync(query, Config);
                    MiniInputText = result;
                    IsAiResultDisplayed = true;
                }
            }
            catch (Exception ex)
            {
                MiniInputText = $"[AI 错误] {ex.Message}";
                IsAiResultDisplayed = true;
            }
            finally
            {
                IsAiProcessing = false;
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

        private static string NormalizeSymbols(string s)
        {
            return new string((s ?? "").Select(NormalizeSymbol).ToArray());
        }

        public async Task ExecuteMiniAiOrPatternAsync()
        {
            var inputText = MiniInputText.Trim();
            if (string.IsNullOrWhiteSpace(inputText)) return;

            IsAiProcessing = true;
            try
            {
                var prefix = LocalConfig.MiniPatternPrefix ?? "";
                var normalizedInput = NormalizeSymbols(inputText);
                var normalizedPrefix = NormalizeSymbols(prefix);

                if (!string.IsNullOrWhiteSpace(prefix) && normalizedInput.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                {
                    var query = inputText.Substring(prefix.Length).TrimStart();
                    if (string.IsNullOrWhiteSpace(query)) return;

                    var patternContent = await _fabricService.FindBestPatternAndContentAsync(query, _aiService, Config);
                    if (!string.IsNullOrEmpty(patternContent))
                    {
                        MiniInputText = $"{patternContent}\n\n---\n\nUSER INPUT:\n{query}";
                    }
                    else
                    {
                        MiniInputText = $"[提示词匹配失败] {query}";
                    }
                    IsAiResultDisplayed = true;
                    return;
                }

                var result = await _aiService.ChatAsync(inputText, Config);
                MiniInputText = result;
                IsAiResultDisplayed = true;
            }
            catch (Exception ex)
            {
                MiniInputText = $"[AI 错误] {ex.Message}";
                IsAiResultDisplayed = true;
            }
            finally
            {
                IsAiProcessing = false;
            }
        }

        private void PerformSearch(string keyword)
        {
            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(keyword)) { foreach (var file in Files.Take(10)) SearchResults.Add(file); }
            else
            {
                var lowerKey = keyword.ToLower();
                var matches = Files.Where(f => f.Title.ToLower().Contains(lowerKey)).Take(10);
                foreach (var m in matches) SearchResults.Add(m);
            }
            if (SearchResults.Count > 0) SelectedSearchItem = SearchResults.FirstOrDefault();
        }

        [RelayCommand]
        private void ConfirmSearchResult()
        {
            if (SelectedSearchItem != null) { MiniInputText = SelectedSearchItem.Content ?? ""; IsSearchPopupOpen = false; }
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
                if (!LocalConfig.MiniPinnedPromptClickShowsFullContent && !string.IsNullOrWhiteSpace(LocalConfig.MiniPinnedPromptId))
                {
                    var pinned = Files.FirstOrDefault(f => f.Id == LocalConfig.MiniPinnedPromptId);
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

        partial void OnSelectedFolderChanged(FolderItem? value)
        {
            FilesView?.Refresh();
            SelectedFile = null;
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
                        FocusMiniInputBox(mainWin);
                    }
                }
                else
                {
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
                FocusMiniInputBox(miniWin);
            }
        }

        public void CaptureForegroundWindow()
        {
            var handle = NativeMethods.GetForegroundWindow();
            if (handle != IntPtr.Zero) _previousWindowHandle = handle;
        }

        [RelayCommand] private void CreateFolder() { var f = new FolderItem { Name = $"新建文件夹 {Folders.Count + 1}" }; Folders.Add(f); SelectedFolder = f; RequestSave(); }
        [RelayCommand] private void CreateFile() { if (SelectedFolder == null) return; _isCreatingFile = true; var f = new PromptItem { Title = "新文档", Content = "# 新文档", FolderId = SelectedFolder.Id, LastModified = DateTime.Now }; Files.Add(f); SelectedFile = f; IsEditMode = true; RequestSave(); _isCreatingFile = false; }
        [RelayCommand] private void DeleteFile(PromptItem? i) { var t = i ?? SelectedFile; if (t != null) { Files.Remove(t); if (SelectedFile == t) SelectedFile = null; RequestSave(); } }
        [RelayCommand]
        private void DeleteFolder(FolderItem? folder)
        {
            if (folder == null) return;

            var filesInFolder = Files.Where(f => f.FolderId == folder.Id).ToList();
            foreach (var file in filesInFolder) Files.Remove(file);

            if (SelectedFolder == folder) SelectedFolder = null;

            Folders.Remove(folder);
            RequestSave();
        }
        [RelayCommand]
        private void ChangeFolderIcon(FolderItem f)
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
        private void RenameFolder(FolderItem f)
        {
            if (f == null) return;
            var dialog = new NameInputDialog(f.Name);
            if (dialog.ShowDialog() == true)
            {
                f.Name = dialog.ResultName;
                RequestSave();
            }
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
        public void ReorderFolders(int o, int n) { Folders.Move(o, n); RequestSave(); }
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
                Folders = new ObservableCollection<FolderItem>(data.Folders);
                Files = new ObservableCollection<PromptItem>(data.Files);

                if (Folders.Count > 0)
                {
                    var fid = Folders.First().Id;
                    foreach (var f in Files) if (string.IsNullOrEmpty(f.FolderId)) f.FolderId = fid;
                }

                var v = CollectionViewSource.GetDefaultView(Files);

                if (v != null) v.Filter = FilterFiles;
                FilesView = v;

                Files.CollectionChanged += (s, e) => RequestSave();
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
