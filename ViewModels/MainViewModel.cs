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
        private readonly IDataService _dataService;
        private readonly GlobalKeyService _keyService;
        private readonly BrowserAutomationService _browserService;
        private readonly AiService _aiService;

        // ★★★ 新增：Fabric 服务 ★★★
        private readonly FabricService _fabricService;

        private bool _isCreatingFile = false;
        private DispatcherTimer _timer;
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

        public MainViewModel()
        {
            // 1. 初始化配置
            Config = ConfigService.Load();
            LocalConfig = LocalConfigService.Load();
            UpdateGlobalHotkey();

            // 2. ★★★ 初始化所有服务 (解决字段为 null 的报错) ★★★
            _dataService = new WebDavDataService(); // 默认使用 WebDav，也可改为 new FileDataService()
            _aiService = new AiService();
            _fabricService = new FabricService();

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

            // 绑定 Ctrl 双击事件 (唤醒/隐藏)
            _keyService.OnDoubleCtrlDetected += (s, e) => Application.Current.Dispatcher.Invoke(() => ToggleMainWindow());

            // ★★★ 绑定双击分号事件 (包含强制焦点修复) ★★★
            // ★★★ 绑定双击分号事件 (包含强制焦点修复 + 防泄漏清理) ★★★
            _keyService.OnDoubleSemiColonDetected += (s, e) => Application.Current.Dispatcher.Invoke(async () =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow == null) return;

                // 1. 唤醒窗口逻辑
                if (!IsFullMode && mainWindow.Visibility == Visibility.Visible && mainWindow.IsActive)
                {
                    MiniInputText = ""; // 已经在前台，仅清空
                }
                else
                {
                    if (mainWindow.Visibility != Visibility.Visible)
                    {
                        ToggleMainWindow(); // 唤醒
                    }
                    MiniInputText = ""; // 先清空一次
                }

                // 2. 强制抢占焦点
                // 给一点延迟等待窗口渲染
                await Task.Delay(50);

                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Topmost = true;

                var interopHelper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                NativeMethods.SetForegroundWindow(interopHelper.Handle);

                mainWindow.MiniInputBox.Focus();
                Keyboard.Focus(mainWindow.MiniInputBox);

                // 3. ★★★ 核心修复：二次清理 ★★★
                // 在获取焦点后，再次检查输入框。如果输入法或系统“漏”进来一个分号，在这里删掉它。
                // 给极短的缓冲时间让“漏”进来的字符上屏
                await Task.Delay(20);

                // 检查并移除开头的分号 (兼容中英文)
                if (!string.IsNullOrEmpty(MiniInputText))
                {
                    // 移除开头的 ; 或 ；
                    string cleaned = MiniInputText.TrimStart(';', '；');

                    // 只有当确实有变化时才赋值，避免光标跳动
                    if (cleaned != MiniInputText)
                    {
                        MiniInputText = cleaned;
                    }
                }

                // 4. 确保光标在最后
                mainWindow.MiniInputBox.CaretIndex = mainWindow.MiniInputBox.Text.Length;
            });

            // 启动按键监听
            if (Config.EnableDoubleCtrl) try { _keyService.Start(); } catch { }

            // 6. 异步加载数据
            _ = InitializeAsync();
        }

        // ★★★ 修改 1：实时检测输入框内容 ★★★
        partial void OnMiniInputTextChanged(string value)
        {
            // 新增：用户开始输入时，清除AI回复标记
            if (IsAiResultDisplayed && !string.IsNullOrWhiteSpace(value))
            {
                IsAiResultDisplayed = false;
            }

            // 1. AI 触发检测：根据配置决定是否需要前缀
            bool needPrefix = !LocalConfig.MiniWindowUseAi;

            if (needPrefix)
            {
                // 需要前缀的检测逻辑
                if (value.StartsWith("ai ", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("ai　", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("''") ||
                    value.StartsWith("''"))
                {
                    IsSearchPopupOpen = false;
                    Variables.Clear();
                    HasVariables = false;
                    IsMiniVarsExpanded = false;
                    return;
                }
            }

            // 2. 补回缺失的逻辑：搜索触发 (/)
            if (value.StartsWith("/") || value.StartsWith("、"))
            {
                string keyword = value.Length > 1 ? value.Substring(1) : "";
                PerformSearch(keyword);
                IsSearchPopupOpen = true;
            }
            else
            {
                IsSearchPopupOpen = false;
            }

            // 3. 补回缺失的逻辑：实时变量解析
            ParseVariablesRealTime(value);
        }

        // ... (BrowserService_OnTargetSiteMatched, UpdateGlobalHotkey 等方法保持不变) ...

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
        public void UpdateGlobalHotkey()
        {
            try
            {
                string hotkeyStr = Config.GlobalHotkey;
                if (string.IsNullOrEmpty(hotkeyStr)) return;
                ModifierKeys modifiers = ModifierKeys.None;
                if (hotkeyStr.Contains("Ctrl")) modifiers |= ModifierKeys.Control;
                if (hotkeyStr.Contains("Alt")) modifiers |= ModifierKeys.Alt;
                if (hotkeyStr.Contains("Shift")) modifiers |= ModifierKeys.Shift;
                if (hotkeyStr.Contains("Win")) modifiers |= ModifierKeys.Windows;
                string keyStr = hotkeyStr.Split('+').Last().Trim();
                if (Enum.TryParse(keyStr, out Key key))
                {
                    try { HotkeyManager.Current.Remove("ToggleWindow"); } catch { }
                    HotkeyManager.Current.AddOrReplace("ToggleWindow", key, modifiers, OnGlobalHotkeyTriggered);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"热键注册失败: {ex.Message}"); }
        }

        private void OnGlobalHotkeyTriggered(object? sender, HotkeyEventArgs e) => ToggleMainWindow();
        // ★★★ 修改：执行 AI 路由与组装逻辑 (选项 B) ★★★
        public async Task ExecuteAiQuery()
        {
            string inputText = MiniInputText.Trim();
            string query = "";

            bool needPrefix = !LocalConfig.MiniWindowUseAi;

            if (needPrefix)
            {
                // 需要前缀的检测逻辑
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
                // 不需要前缀，直接使用全部输入
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

        // ... (其余代码完全保持不变) ...

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
            if (IsFullMode) finalContent = SelectedFile?.Content ?? "";
            else finalContent = MiniInputText;

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

            // 1. 先隐藏窗口
            if (window != null) window.Hide();

            // 2. 执行发送（模拟剪贴板粘贴）
            await InputSender.SendAsync(content, targetMode, LocalConfig, _previousWindowHandle);

            // 3. 发送完成后清理输入框
            if (!IsFullMode)
            {
                MiniInputText = ""; // 清空输入框

                // ★★★ 修改：注释掉或删除下面的“重新显示”逻辑 ★★★
                // 原来的逻辑是发送完自动弹回来，现在注释掉，让它保持隐藏。
                /*
                await Task.Delay(100);
                if (window != null)
                {
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                    window.Topmost = true;
                    window.Focus();
                    if (window is MainWindow mainWin)
                    {
                        await mainWin.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            mainWin.MiniInputBox.Focus();
                        }), DispatcherPriority.Render);
                    }
                }
                */
            }
            else
            {
                // 如果是完整模式，通常也清空附加输入框
                AdditionalInput = "";
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
            if (e.PropertyName == nameof(PromptItem.Content) && IsFullMode) ParseVariablesRealTime(SelectedFile?.Content ?? "");
            RequestSave();
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
                if (!IsFullMode && window is MainWindow mainWin)
                {
                    mainWin.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        mainWin.MiniInputBox.Focus();
                    }), DispatcherPriority.Render);
                }
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
        [RelayCommand] private void DeleteFolder(FolderItem? folder)
        {
            if (folder == null) return;

            var filesInFolder = Files.Where(f => f.FolderId == folder.Id).ToList();
            foreach (var file in filesInFolder) Files.Remove(file);

            if (SelectedFolder == folder) SelectedFolder = null;

            Folders.Remove(folder);
            RequestSave();
        }
        [RelayCommand] private void ChangeFolderIcon(FolderItem f)
        {
            if (f == null) return;
            var dialog = new IconInputDialog(f.IconGeometry);
            if (dialog.ShowDialog() == true)
            {
                f.IconGeometry = dialog.ResultGeometry;
                RequestSave();
            }
        }
        [RelayCommand] private void RenameFolder(FolderItem f) { /* ... */ }
        [RelayCommand] private void ChangeFileIcon(PromptItem f)
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
        [RelayCommand] private void SaveSettings() { ConfigService.Save(Config); LocalConfigService.Save(LocalConfig); UpdateGlobalHotkey(); if (Config.EnableDoubleCtrl) try { _keyService.Start(); } catch { } else _keyService.Stop(); IsSettingsOpen = false; }
        [RelayCommand] private void SelectSettingsTab(string s) { if (int.TryParse(s, out int i)) SelectedSettingsTab = i; }
        public void ReorderFolders(int o, int n) { Folders.Move(o, n); RequestSave(); }
        public void MoveFileToFolder(PromptItem f, FolderItem t) { if (f == null || t == null || f.FolderId == t.Id) return; f.FolderId = t.Id; FilesView?.Refresh(); if (SelectedFile == f) SelectedFile = null; RequestSave(); }
        [RelayCommand] private void ToggleNavigation() => IsNavigationVisible = !IsNavigationVisible;
        [RelayCommand] private void ToggleEditMode() { IsEditMode = !IsEditMode; if (!IsEditMode) RequestSave(); }
        [RelayCommand] private void CopyCompiledText() { /* ... */ }
        [RelayCommand] private async Task SendDirectPrompt() { await SendFromMini("SmartFocus"); }
        [RelayCommand] private async Task SendCombinedInput() { await SendFromMini("SmartFocus"); }
        [RelayCommand] private async Task ManualBackup() { ConfigService.Save(Config); try { await _dataService.SaveAsync(Folders, Files); _lastSyncTime = DateTime.Now; MessageBox.Show("备份成功"); } catch (Exception e) { MessageBox.Show(e.Message); } }
        [RelayCommand] private async Task ManualRestore() { /* ... */ }

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

        private async Task InitializeAsync()
        {
            var data = await _dataService.LoadAsync();
            if (data.Folders.Count == 0) data.Folders.Add(new FolderItem { Name = "我的提示词" });
            Folders = new ObservableCollection<FolderItem>(data.Folders);
            Files = new ObservableCollection<PromptItem>(data.Files);
            var fid = Folders.First().Id;
            foreach (var f in Files) if (string.IsNullOrEmpty(f.FolderId)) f.FolderId = fid;
            var v = CollectionViewSource.GetDefaultView(Files);

            if (v != null) v.Filter = FilterFiles;
            FilesView = v;

            Files.CollectionChanged += (s, e) => RequestSave();
            SelectedFolder = Folders.FirstOrDefault();
            IsFullMode = true;
        }

        private bool FilterFiles(object o) => o is PromptItem f && SelectedFolder != null && f.FolderId == SelectedFolder.Id;
        private async void RequestSave() { if (!string.IsNullOrEmpty(Config.UserName)) await SaveDataAsync(); }
        private async Task SaveDataAsync() { try { await _dataService.SaveAsync(Folders, Files); _lastSyncTime = DateTime.Now; UpdateTimeDisplay(); } catch { SyncTimeDisplay = "Err"; } }
        private void UpdateTimeDisplay() { var s = DateTime.Now - _lastSyncTime; SyncTimeDisplay = s.TotalSeconds < 60 ? $"{(int)s.TotalSeconds}s" : $"{(int)s.TotalMinutes}m"; }
    }
}