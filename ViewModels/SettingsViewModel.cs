using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv6.ViewModels
{
    /// <summary>
    /// 设置相关的 ViewModel
    /// 负责管理应用程序配置、AI模型、同步、主题、快捷键等设置功能
    /// 从 MainViewModel 中拆分出来以降低耦合度
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IAiService _aiService;
        private readonly IDataService _dataService;
        private readonly FileDataService _localDataService;
        private readonly GlobalKeyService _keyService;
        private readonly IDialogService _dialogService;
        private readonly HotkeyService _hotkeyService;
        private readonly BaiduService _baiduService;
        private readonly TencentService _tencentService;
        private readonly GoogleService _googleService;
        private readonly IWindowManager _windowManager;

        // 引用 MainViewModel 以访问 Files、Folders 等数据（用于同步恢复）
        // 这是暂时的依赖，后续可以通过消息总线进一步解耦
        private MainViewModel? _mainViewModel;

        public void SetMainViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        #region Observable Properties - UI State

        [ObservableProperty] private bool isSettingsOpen;
        [ObservableProperty] private int selectedSettingsTab;
        [ObservableProperty] private bool isNavigationVisible = true;

        #endregion

        #region Observable Properties - AI Model Management

        [ObservableProperty] private string? testStatus;
        [ObservableProperty] private System.Windows.Media.Brush testStatusColor = System.Windows.Media.Brushes.Gray;

        [ObservableProperty] private AiModelConfig? selectedSavedModel;

        partial void OnSelectedSavedModelChanged(AiModelConfig? value)
        {
            // Logic removed: We no longer sync to Config.Ai* fields automatically.
            // Editing is done directly on the SelectedSavedModel via UI bindings.
        }

        #endregion



        #region Observable Properties - Sync & Restore

        [ObservableProperty] private bool isRestoreConfirmVisible;
        [ObservableProperty] private string? restoreStatus;
        [ObservableProperty] private System.Windows.Media.Brush restoreStatusColor = System.Windows.Media.Brushes.Green;

        #endregion

        #region Observable Properties - Baidu Credentials

        // 百度 OCR 凭据
        [ObservableProperty] private string? baiduOcrApiKey;
        [ObservableProperty] private string? baiduOcrSecretKey;

        // 百度翻译凭据
        [ObservableProperty] private string? baiduTranslateAppId;
        [ObservableProperty] private string? baiduTranslateSecretKey;

        #endregion

        #region Observable Properties - Tencent Credentials

        // 腾讯云 OCR 凭据
        [ObservableProperty] private string? tencentOcrSecretId;
        [ObservableProperty] private string? tencentOcrSecretKey;

        // 腾讯云翻译凭据
        [ObservableProperty] private string? tencentTranslateSecretId;
        [ObservableProperty] private string? tencentTranslateSecretKey;

        #endregion

        #region Observable Properties - Youdao Credentials

        // 有道 OCR 凭据
        [ObservableProperty] private string? youdaoOcrAppKey;
        [ObservableProperty] private string? youdaoOcrAppSecret;

        // 有道翻译凭据
        [ObservableProperty] private string? youdaoTranslateAppKey;
        [ObservableProperty] private string? youdaoTranslateAppSecret;

        #endregion

        #region Observable Properties - Google Credentials

        // Google 翻译凭据
        [ObservableProperty] private string? googleBaseUrl;
        [ObservableProperty] private string? googleApiKey;

        #endregion

        #region Observable Properties - OCR & Translation Test Status

        // 百度 OCR 测试
        [ObservableProperty] private string? baiduOcrTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush baiduOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

        // 百度翻译测试
        [ObservableProperty] private string? baiduTranslateTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush baiduTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

        // 腾讯云 OCR 测试
        [ObservableProperty] private string? tencentOcrTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush tencentOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

        // 腾讯云翻译测试
        [ObservableProperty] private string? tencentTranslateTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush tencentTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

        // 有道测试
        [ObservableProperty] private string? youdaoTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush youdaoTestStatusColor = System.Windows.Media.Brushes.Gray;

        // Google 测试
        [ObservableProperty] private string? googleTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush googleTestStatusColor = System.Windows.Media.Brushes.Gray;

        #endregion

        #region Configuration Access (通过 SettingsService)

        public AppConfig Config => _settingsService.Config;
        public LocalSettings LocalConfig => _settingsService.LocalConfig;

        #endregion

        public SettingsViewModel(
            ISettingsService settingsService,
            IAiService aiService,
            IDataService dataService,
            FileDataService localDataService,
            GlobalKeyService keyService,
            IDialogService dialogService,
            BaiduService baiduService,
            TencentService tencentService,
            GoogleService googleService,
            IWindowManager windowManager)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _dataService = dataService;
            _localDataService = localDataService;
            _keyService = keyService;
            _dialogService = dialogService;
            _hotkeyService = new HotkeyService();
            _baiduService = baiduService;
            _tencentService = tencentService;
            _googleService = googleService;
            _windowManager = windowManager;

            // 加载凭据到 UI 绑定的属性
            LoadBaiduCredentials();
            LoadTencentCredentials();
            LoadYoudaoCredentials();
            LoadGoogleCredentials();

            LoggerService.Instance.LogInfo("SettingsViewModel initialized", "SettingsViewModel.ctor");
        }

        #region Commands - Settings UI

        [RelayCommand]
        private void OpenSettings()
        {
            IsSettingsOpen = true;
        }

        [RelayCommand]
        private void CloseSettings()
        {
            IsSettingsOpen = false;
            _settingsService.SaveConfig();
            _settingsService.SaveLocalConfig();
            if (_mainViewModel != null)
            {
                _windowManager.CloseWindow(_mainViewModel);
            }
        }

        [RelayCommand]
        private void ToggleNavigation()
        {
            IsNavigationVisible = !IsNavigationVisible;
        }

        #endregion

        #region Commands - AI Model Management

        [ObservableProperty] private string? translationTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush translationTestStatusColor = System.Windows.Media.Brushes.Gray;

        [RelayCommand]
        private async Task TestAiTranslationConnection()
        {
            var enabledModels = Config.SavedModels.Where(m => m.IsEnableForTranslation).ToList();
            if (enabledModels.Count == 0)
            {
                TranslationTestStatus = "请先勾选至少一个参与翻译的 AI 模型";
                TranslationTestStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            TranslationTestStatus = "测试中...";
            TranslationTestStatusColor = System.Windows.Media.Brushes.Gray;

            int successCount = 0;
            string lastError = "";
            long totalTime = 0;

            foreach (var model in enabledModels)
            {
                var result = await _aiService.TestConnectionAsync(model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);
                if (result.Success)
                {
                    successCount++;
                    if (result.ResponseTimeMs.HasValue)
                        totalTime += result.ResponseTimeMs.Value;
                }
                else lastError = result.Message;
            }

            if (successCount == enabledModels.Count)
            {
                var avgTime = successCount > 0 ? totalTime / successCount : 0;
                TranslationTestStatus = $"全部 {successCount} 个模型连接成功 (平均 {avgTime}ms)";
                TranslationTestStatusColor = System.Windows.Media.Brushes.Green;
            }
            else if (successCount > 0)
            {
                TranslationTestStatus = $"部分成功 ({successCount}/{enabledModels.Count})\n失败示例: {lastError}";
                TranslationTestStatusColor = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                TranslationTestStatus = $"全部失败。\n错误示例: {lastError}";
                TranslationTestStatusColor = System.Windows.Media.Brushes.Red;
            }
        }

        [RelayCommand]
        private async Task TestAiConnection(AiModelConfig? model)
        {
            if (model == null)
            {
                 var (success, msg, timeMs) = await _aiService.TestConnectionAsync(Config);
                 TestStatus = success && timeMs.HasValue ? $"{msg} ({timeMs}ms)" : msg;
                 TestStatusColor = success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                 return;
            }

            TestStatus = "测试中...";
            TestStatusColor = System.Windows.Media.Brushes.Gray;
            var (success2, message, responseTimeMs) = await _aiService.TestConnectionAsync(model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);
            TestStatus = success2 && responseTimeMs.HasValue ? $"{message} ({responseTimeMs}ms)" : message;
            TestStatusColor = success2 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        [RelayCommand]
        private void AddAiModel()
        {
            var newModel = new AiModelConfig
            {
                Id = Guid.NewGuid().ToString(),
                ModelName = "gpt-3.5-turbo",
                BaseUrl = "https://api.openai.com/v1",
                ApiKey = "",
                Remark = "New Model"
            };

            Config.SavedModels.Insert(0, newModel);
            
            // Select it for editing
            SelectedSavedModel = newModel;
            
            // Optionally set as active if it's the first one
            if (string.IsNullOrEmpty(Config.ActiveModelId))
            {
                Config.ActiveModelId = newModel.Id;
            }
            
            _settingsService.SaveConfig();
        }

        [RelayCommand]
        private void ConfirmDeleteAiModel(AiModelConfig? model)
        {
            if (model == null) return;
            model.IsPendingDelete = true;
        }

        [RelayCommand]
        private void DeleteAiModel(AiModelConfig? model)
        {
            if (model == null) return;
            var idx = Config.SavedModels.IndexOf(model);
            if (idx >= 0) Config.SavedModels.RemoveAt(idx);
            
            // If we deleted the active model, clear the ID
            if (Config.ActiveModelId == model.Id) Config.ActiveModelId = "";
            
            // If we deleted the selected model, clear selection
            if (SelectedSavedModel == model) SelectedSavedModel = null;
            
            _settingsService.SaveConfig();
        }

        [RelayCommand]
        private void RenameAiModel(AiModelConfig? model)
        {
            if (model == null) return;

            string initialName = string.IsNullOrWhiteSpace(model.Remark) ? model.ModelName : model.Remark;
            var dialog = new NameInputDialog(initialName);
            if (dialog.ShowDialog() == true)
            {
                model.Remark = dialog.ResultName;
                _settingsService.SaveConfig();
            }
        }

        #endregion

        #region Commands - Hotkey Management

        public void UpdateWindowHotkeys()
        {
            try
            {
                _hotkeyService.TryRemoveHotkey("ToggleWindow");
                _hotkeyService.TryRemoveHotkey("ToggleWindowSingle");

                // Register Full window hotkey using helper method
                _hotkeyService.RegisterWindowHotkey("ToggleFullWindowHotkey", Config.FullWindowHotkey, () => _mainViewModel?.OnWindowHotkeyPressed());
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to update window hotkeys", "SettingsViewModel.UpdateWindowHotkeys");
            }
        }

        public void UpdateExternalToolsHotkeys()
        {
            if (_mainViewModel == null) return;

            try 
            {
                // Remove old hotkeys
                _hotkeyService.TryRemoveHotkey("ScreenshotTranslate");
                _hotkeyService.TryRemoveHotkey("OcrOnly");
                _hotkeyService.TryRemoveHotkey("PinToScreen");

                // Register new hotkeys from external tools settings
                _hotkeyService.RegisterWindowHotkey("ScreenshotTranslate", Config.ScreenshotTranslateHotkey, () => _mainViewModel.ExternalToolsVM.TriggerTranslateCommand.Execute(null));
                _hotkeyService.RegisterWindowHotkey("OcrOnly", Config.OcrHotkey, () => _mainViewModel.ExternalToolsVM.TriggerOcrCommand.Execute(null));
                _hotkeyService.RegisterWindowHotkey("PinToScreen", Config.PinToScreenHotkey, () => _mainViewModel.ExternalToolsVM.TriggerPinToScreenCommand.Execute(null));
                
                // Update Launcher Hotkey
                UpdateLauncherHotkey();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to update external tools hotkeys", "SettingsViewModel.UpdateExternalToolsHotkeys");
            }
        }

        public void UpdateLauncherHotkey()
        {
            try
            {
                _keyService.LauncherHotkeyString = Config.LauncherHotkey;
                // 注意：此处不调用 SaveConfig()，调用方负责在需要时保存
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to update launcher hotkey", "SettingsViewModel.UpdateLauncherHotkey");
            }
        }

        #endregion

        #region Commands - Launcher Management

        [RelayCommand]
        private void AddLauncherSearchPath()
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择要添加的搜索文件夹",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.SelectedPath;
                    if (!Config.LauncherSearchPaths.Contains(path))
                    {
                        Config.LauncherSearchPaths.Add(path);
                        _settingsService.SaveConfig();
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAlert($"添加文件夹失败: {ex.Message}", "错误");
                LoggerService.Instance.LogException(ex, "Failed to add launcher search path", "SettingsViewModel.AddLauncherSearchPath");
            }
        }

        [RelayCommand]
        private void RemoveLauncherSearchPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            Config.LauncherSearchPaths.Remove(path);
            _settingsService.SaveConfig();
        }

        #endregion

        #region Commands - Sync & Restore

        [RelayCommand]
        private void ShowRestoreConfirm()
        {
            IsRestoreConfirmVisible = true;
            RestoreStatus = null;
        }

        [RelayCommand]
        private void CancelRestoreConfirm()
        {
            IsRestoreConfirmVisible = false;
        }

        [RelayCommand]
        private async Task ManualRestore()
        {
            if (_mainViewModel == null)
            {
                _dialogService.ShowAlert("系统初始化未完成", "错误");
                return;
            }

            // Ensure current config (especially WebDAV credentials) is saved to disk
            // because DataService reads from disk
            _settingsService.SaveConfig();

            IsRestoreConfirmVisible = false;
            RestoreStatus = "正在从云端恢复数据...";
            RestoreStatusColor = System.Windows.Media.Brushes.Orange;

            try
            {
                var data = await _dataService.LoadAsync();

                if (data == null || ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0))
                {
                    // It's possible it's really empty, or it failed silently (WebDavDataService swallows errors)
                    // But if it's empty, we treat it as "No data found"
                    RestoreStatus = "❌ 云端没有数据可恢复 (或连接失败)";
                    RestoreStatusColor = System.Windows.Media.Brushes.Red;
                    return;
                }

                // Clear current data
                _mainViewModel.Files.Clear();
                _mainViewModel.SidebarVM.Folders.Clear();

                // Restore folders
                if (data.Folders != null && data.Folders.Count > 0)
                {
                    foreach (var folder in data.Folders)
                    {
                        _mainViewModel.SidebarVM.Folders.Add(folder);
                    }
                    _mainViewModel.SidebarVM.SelectedFolder = _mainViewModel.SidebarVM.Folders.FirstOrDefault();
                }
                else
                {
                    var defaultFolder = new FolderItem { Name = "默认" };
                    _mainViewModel.SidebarVM.Folders.Add(defaultFolder);
                    _mainViewModel.SidebarVM.SelectedFolder = defaultFolder;
                }

                // Restore files
                if (data.Files != null && data.Files.Count > 0)
                {
                    foreach (var file in data.Files)
                    {
                        // Ensure FolderId integrity
                        if (string.IsNullOrWhiteSpace(file.FolderId) && _mainViewModel.SidebarVM.SelectedFolder != null)
                        {
                            file.FolderId = _mainViewModel.SidebarVM.SelectedFolder.Id;
                        }
                        _mainViewModel.Files.Add(file);
                    }
                }

                // Update view (调用 MainViewModel 的方法)
                _mainViewModel.SidebarVM.Files = _mainViewModel.Files;
                _mainViewModel.UpdateFilesViewFilter();
                _mainViewModel.FilesView?.Refresh();

                // Save to local
                await _mainViewModel.PerformLocalBackup();

                RestoreStatus = $"✅ 成功恢复 {data.Folders?.Count ?? 0} 个文件夹和 {data.Files?.Count ?? 0} 个提示词";
                RestoreStatusColor = System.Windows.Media.Brushes.Green;

                LoggerService.Instance.LogInfo($"Restored {data.Folders?.Count ?? 0} folders and {data.Files?.Count ?? 0} files", "SettingsViewModel.ManualRestore");
            }
            catch (Exception ex)
            {
                RestoreStatus = $"❌ 恢复失败: {ex.Message}";
                RestoreStatusColor = System.Windows.Media.Brushes.Red;
                LoggerService.Instance.LogException(ex, "Failed to restore from cloud", "SettingsViewModel.ManualRestore");
            }
        }

        [RelayCommand]
        private async Task ManualLocalRestore()
        {
            if (_mainViewModel == null)
            {
                _dialogService.ShowAlert("系统初始化未完成", "错误");
                return;
            }

            // 1. Get backups
            var service = _localDataService as FileDataService;
            if (service == null) return;

            var backups = service.GetBackups();
            LoggerService.Instance.LogInfo($"Found {backups.Count} backups in {service.BackupDirectory}", "SettingsViewModel.ManualLocalRestore");

            if (backups.Count == 0)
            {
                _dialogService.ShowToast($"在以下路径未找到本地备份文件：\n{service.BackupDirectory}\n\n请确保已进行过保存操作。", "Warning");
                return;
            }

            // 2. Show dialog
            var dialog = new Views.BackupSelectionDialog(backups);
            
            // Try to set owner to the active window first, fallback to MainWindow
            var activeWindow = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            dialog.Owner = activeWindow ?? System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() != true || dialog.SelectedBackup == null) return;

            // 3. Confirm
            var selected = dialog.SelectedBackup;
            if (!_dialogService.ShowConfirmation($"确定要恢复到备份点：\n{selected.DisplayText} 吗？\n当前未保存的更改将会丢失。", "确认恢复"))
            {
                return;
            }

            // 4. Perform Restore
            RestoreStatus = "正在恢复本地数据...";
            RestoreStatusColor = System.Windows.Media.Brushes.Orange;

            try
            {
                var data = await service.RestoreLocalBackupAsync(selected.FilePath);
                if (data == null)
                {
                    throw new Exception("读取备份文件失败");
                }

                // Restore logic (same as cloud restore basically)
                // Clear current data
                _mainViewModel.Files.Clear();
                _mainViewModel.SidebarVM.Folders.Clear();

                // Restore folders
                if (data.Folders != null && data.Folders.Count > 0)
                {
                    foreach (var folder in data.Folders)
                    {
                        _mainViewModel.SidebarVM.Folders.Add(folder);
                    }
                    _mainViewModel.SidebarVM.SelectedFolder = _mainViewModel.SidebarVM.Folders.FirstOrDefault();
                }
                else
                {
                    var defaultFolder = new FolderItem { Name = "默认" };
                    _mainViewModel.SidebarVM.Folders.Add(defaultFolder);
                    _mainViewModel.SidebarVM.SelectedFolder = defaultFolder;
                }

                // Restore files
                if (data.Files != null && data.Files.Count > 0)
                {
                    foreach (var file in data.Files)
                    {
                        _mainViewModel.Files.Add(file);
                    }
                }

                // Update view
                _mainViewModel.SidebarVM.Files = _mainViewModel.Files;
                _mainViewModel.UpdateFilesViewFilter();
                _mainViewModel.FilesView?.Refresh();

                RestoreStatus = $"✅ 本地恢复成功: {selected.FileName}";
                RestoreStatusColor = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                RestoreStatus = $"❌ 恢复失败: {ex.Message}";
                RestoreStatusColor = System.Windows.Media.Brushes.Red;
            }
        }

        [RelayCommand]
        private async Task ManualBackup()
        {
            if (_mainViewModel == null)
            {
                _dialogService.ShowAlert("系统初始化未完成", "错误");
                return;
            }

            try
            {
                await _dataService.SaveAsync(_mainViewModel.SidebarVM.Folders, _mainViewModel.Files);
                _mainViewModel.LocalConfig.LastCloudSyncTime = DateTime.Now; // Update sync time
                _mainViewModel.IsDirty = false; // Reset dirty state indicator
                _mainViewModel.IsEditMode = false; // Switch to preview mode on successful backup
                _settingsService.SaveLocalConfig(); // Persist the sync time
                LoggerService.Instance.LogInfo("Manual cloud backup successful", "SettingsViewModel.ManualBackup");
            }
            catch (Exception ex)
            {
                _dialogService.ShowAlert($"备份失败: {ex.Message}", "错误");
                LoggerService.Instance.LogException(ex, "Failed to manual backup", "SettingsViewModel.ManualBackup");
            }
        }

        #endregion

        #region Commands - Log Management

        [RelayCommand]
        private void OpenLogFolder()
        {
            try
            {
                var logPath = LoggerService.Instance.GetLogDirectory();
                if (System.IO.Directory.Exists(logPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logPath);
                }
                else
                {
                    _dialogService.ShowToast("日志文件夹不存在", "Info");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to open log folder", "SettingsViewModel.OpenLogFolder");
                _dialogService.ShowAlert($"无法打开日志文件夹: {ex.Message}", "错误");
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            try
            {
                LoggerService.Instance.ClearLogs();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to clear logs", "SettingsViewModel.ClearLogs");
                _dialogService.ShowAlert($"清除日志失败: {ex.Message}", "错误");
            }
        }

        #endregion

        #region Commands - Config Export/Import

        [RelayCommand]
        private void ExportConfig()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出配置",
                Filter = "配置文件压缩包 (*.zip)|*.zip",
                FileName = $"PromptMasterv6_Config_{DateTime.Now:yyyyMMdd_HHmm}.zip"
            };

            var owner = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive);

            if (dialog.ShowDialog(owner) == true)
            {
                try
                {
                    _settingsService.ExportSettings(dialog.FileName);
                    _dialogService.ShowToast("配置导出成功！", "Success");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowAlert($"配置导出失败: {ex.Message}", "错误");
                }
            }
        }

        [RelayCommand]
        private void ImportConfig()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "导入配置",
                Filter = "配置文件压缩包 (*.zip)|*.zip"
            };

            var owner = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive);

            if (dialog.ShowDialog(owner) == true)
            {
                if (_dialogService.ShowConfirmation("导入配置将覆盖当前的设置，确定要继续吗？\n(操作后将自动重启生效)", "确认导入"))
                {
                    try
                    {
                        _settingsService.ImportSettings(dialog.FileName);
                        
                        // 重新加载凭据到 UI 绑定的属性
                        LoadBaiduCredentials();
                        LoadTencentCredentials();
                        LoadYoudaoCredentials();
                        LoadGoogleCredentials();
                        
                        // 尝试重新应用设置
                        UpdateWindowHotkeys();
                        UpdateExternalToolsHotkeys();
                        
                        // 刷新外部工具配置
                        if (_mainViewModel?.ExternalToolsVM != null)
                        {
                            _mainViewModel.ExternalToolsVM.RefreshProfiles();
                        }
                        
                        // 由于配置可能发生彻底变化，建议用户重启或重新初始化一些状态
                        // 这里我们刷新一下当前 ViewState
                        OnPropertyChanged(nameof(Config));
                        OnPropertyChanged(nameof(LocalConfig));

                        _dialogService.ShowToast("配置导入成功！", "Success");
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowAlert($"配置导入失败: {ex.Message}", "错误");
                    }
                }
            }
        }

        #endregion

        #region Commands - Baidu API Testing

        /// <summary>
        /// 测试百度 OCR 连接
        /// </summary>
        [RelayCommand]
        private async Task TestBaiduOcr()
        {
            // 先保存凭据到 Config
            SaveBaiduCredentials();

            // Find Baidu OCR profile
            var profile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                BaiduOcrTestStatus = "请先填写 API Key 和 Secret Key";
                BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            BaiduOcrTestStatus = "测试中...";
            BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

            try
            {
                // Test with a minimal white 1x1 PNG image
                byte[] testImage = CreateTestImage();
                var result = await _baiduService.OcrAsync(testImage, profile);

                if (result.StartsWith("错误") || result.Contains("错误"))
                {
                    BaiduOcrTestStatus = $"连接失败：{result}";
                    BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    BaiduOcrTestStatus = "连接成功！";
                    BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                BaiduOcrTestStatus = $"测试出错: {ex.Message}";
                BaiduOcrTestStatusColor = System.Windows.Media.Brushes.Red;
                LoggerService.Instance.LogException(ex, "Failed to test Baidu OCR", "SettingsViewModel.TestBaiduOcr");
            }
        }

        /// <summary>
        /// 测试百度翻译连接
        /// </summary>
        [RelayCommand]
        private async Task TestBaiduTranslate()
        {
            // 先保存凭据到 Config
            SaveBaiduCredentials();

            // Find Baidu Translation profile
            var profile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                BaiduTranslateTestStatus = "请先填写 App ID 和 Secret Key";
                BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            BaiduTranslateTestStatus = "测试中...";
            BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

            try
            {
                // Test with a simple English phrase
                var result = await _baiduService.TranslateAsync("Hello", profile, "en", "zh");

                if (result.StartsWith("错误") || result.Contains("错误") || result.Contains("异常"))
                {
                    BaiduTranslateTestStatus = $"连接失败：{result}";
                    BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    BaiduTranslateTestStatus = $"连接成功！翻译结果：{result}";
                    BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                BaiduTranslateTestStatus = $"测试出错: {ex.Message}";
                BaiduTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
                LoggerService.Instance.LogException(ex, "Failed to test Baidu Translate", "SettingsViewModel.TestBaiduTranslate");
            }
        }

        /// <summary>
        /// 创建测试图片 (用于 OCR 测试)
        /// </summary>
        private byte[] CreateTestImage()
        {
            // Create a valid image with text to satisfy OCR requirements (min size and content)
            var width = 200;
            var height = 60;
            var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                // Background
                context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));

                // Text
                var formattedText = new FormattedText(
                    "OCR TEST",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    24,
                    System.Windows.Media.Brushes.Black,
                    1.0); // PixelsPerDip

                context.DrawText(formattedText, new System.Windows.Point(40, 15));
            }

            renderBitmap.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        #endregion

        #region Commands - Tencent Cloud API Testing

        [RelayCommand]
        private async Task TestTencentOcr()
        {
            SaveTencentCredentials();

            var profile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                TencentOcrTestStatus = "请先填写 Secret ID 和 Secret Key";
                TencentOcrTestStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            TencentOcrTestStatus = "测试中...";
            TencentOcrTestStatusColor = System.Windows.Media.Brushes.Gray;

            try
            {
                var testImage = CreateTestImage();
                var result = await _tencentService.OcrAsync(testImage, profile);

                if (result.StartsWith("Error") || result.StartsWith("Tencent Error"))
                {
                    TencentOcrTestStatus = $"连接失败：{result}";
                    TencentOcrTestStatusColor = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    TencentOcrTestStatus = "连接成功！";
                    TencentOcrTestStatusColor = System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                TencentOcrTestStatus = $"测试出错: {ex.Message}";
                TencentOcrTestStatusColor = System.Windows.Media.Brushes.Red;
                LoggerService.Instance.LogException(ex, "Failed to test Tencent OCR", "SettingsViewModel.TestTencentOcr");
            }
        }

        [RelayCommand]
        private async Task TestTencentCloud()
        {
            SaveTencentCredentials();

            var profile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                TencentTranslateTestStatus = "请先填写 Secret ID 和 Secret Key";
                TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            TencentTranslateTestStatus = "测试中...";
            TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;

            try
            {
                var result = await _tencentService.TranslateAsync("Hello", profile, "auto", "zh");

                if (result.StartsWith("Error") || result.StartsWith("Tencent Error"))
                {
                    TencentTranslateTestStatus = $"连接失败：{result}";
                    TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    TencentTranslateTestStatus = $"连接成功！翻译结果：{result}";
                    TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                TencentTranslateTestStatus = $"测试出错: {ex.Message}";
                TencentTranslateTestStatusColor = System.Windows.Media.Brushes.Red;
                LoggerService.Instance.LogException(ex, "Failed to test Tencent Cloud", "SettingsViewModel.TestTencentCloud");
            }
        }

        #endregion

        #region Commands - Youdao API Testing

        [RelayCommand]
        private void TestYoudao()
        {
            SaveYoudaoCredentials();
            
            var profile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.Translation);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                YoudaoTestStatus = "请先填写 App Key 和 App Secret";
                YoudaoTestStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            YoudaoTestStatus = "有道连接测试功能将在未来版本中实现";
            YoudaoTestStatusColor = System.Windows.Media.Brushes.Orange;
        }

        #endregion

        #region Commands - Google API Testing

        [RelayCommand]
        private async Task TestGoogle()
        {
            SaveGoogleCredentials();

            var profile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1))
            {
                GoogleTestStatus = "请先填写 API Key";
                GoogleTestStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            GoogleTestStatus = "测试中...";
            GoogleTestStatusColor = System.Windows.Media.Brushes.Gray;

            try
            {
                var result = await _googleService.TranslateAsync("Hello World", profile);

                if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Google") && !result.StartsWith("错误") && !result.StartsWith("Google API 错误"))
                {
                    GoogleTestStatus = $"连接成功！翻译结果：{result}";
                    GoogleTestStatusColor = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    GoogleTestStatus = $"连接失败：{result}";
                    GoogleTestStatusColor = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                GoogleTestStatus = $"测试出错: {ex.Message}";
                GoogleTestStatusColor = System.Windows.Media.Brushes.Red;
                LoggerService.Instance.LogException(ex, "Failed to test Google", "SettingsViewModel.TestGoogle");
            }
        }

        #endregion

        #region Commands - AI Translation Config

        [RelayCommand]
        private void JumpToEditPrompt()
        {
            var promptId = Config.AiTranslationPromptId;
            if (string.IsNullOrWhiteSpace(promptId)) return;
            if (_mainViewModel == null) return;

            var prompt = _mainViewModel.Files.FirstOrDefault(f => f.Id == promptId);
            if (prompt != null)
            {
                _mainViewModel.SelectedFile = prompt;
                _mainViewModel.IsEditMode = true;
                _mainViewModel.SaveSettingsCommand.Execute(null);
            }
        }

        [RelayCommand]
        private void SaveAiTranslationConfig()
        {
            var promptId = Config.AiTranslationPromptId;
            var promptTitle = "";
            if (!string.IsNullOrWhiteSpace(promptId))
            {
                var prompt = _mainViewModel?.Files.FirstOrDefault(f => f.Id == promptId);
                promptTitle = prompt?.Title ?? "";
            }

            var config = new AiTranslationConfig
            {
                PromptId = promptId,
                PromptTitle = promptTitle,
                BaseUrl = Config.AiBaseUrl,
                ApiKey = Config.AiApiKey,
                Model = Config.AiModel
            };

            Config.SavedAiTranslationConfigs.Add(config);
            _settingsService.SaveConfig();
            _dialogService.ShowToast("AI 翻译配置已保存！", "Success");
        }

        [RelayCommand]
        private void DeleteAiTranslationConfig(string? configId)
        {
            if (string.IsNullOrWhiteSpace(configId)) return;

            var config = Config.SavedAiTranslationConfigs.FirstOrDefault(c => c.Id == configId);
            if (config != null)
            {
                Config.SavedAiTranslationConfigs.Remove(config);
                _settingsService.SaveConfig();
            }
        }

        #endregion

        #region Private Methods - Credentials Management

        /// <summary>
        /// 加载百度凭据（从 ApiProfiles 加载到 ObservableProperty）
        /// </summary>
        private void LoadBaiduCredentials()
        {
            var baiduOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);
            var baiduTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

            // 加载 OCR 凭据
            if (baiduOcrProfile != null)
            {
                BaiduOcrApiKey = baiduOcrProfile.Key1;
                BaiduOcrSecretKey = baiduOcrProfile.Key2;
            }

            // 加载翻译凭据
            if (baiduTransProfile != null)
            {
                BaiduTranslateAppId = baiduTransProfile.Key1;
                BaiduTranslateSecretKey = baiduTransProfile.Key2;
            }
        }

        /// <summary>
        /// 保存百度凭据（从 ObservableProperty 保存到 ApiProfiles）
        /// </summary>
        private void SaveBaiduCredentials()
        {
            // Find or create Baidu OCR profile
            var baiduOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);

            if (baiduOcrProfile == null)
            {
                baiduOcrProfile = new ApiProfile
                {
                    Name = "百度 OCR",
                    Provider = ApiProvider.Baidu,
                    ServiceType = ServiceType.OCR
                };
                Config.ApiProfiles.Add(baiduOcrProfile);
            }

            // Update OCR credentials
            baiduOcrProfile.Key1 = BaiduOcrApiKey ?? "";
            baiduOcrProfile.Key2 = BaiduOcrSecretKey ?? "";

            // Find or create Baidu Translation profile
            var baiduTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

            if (baiduTransProfile == null)
            {
                baiduTransProfile = new ApiProfile
                {
                    Name = "百度翻译",
                    Provider = ApiProvider.Baidu,
                    ServiceType = ServiceType.Translation
                };
                Config.ApiProfiles.Add(baiduTransProfile);
            }

            // Update Translation credentials
            baiduTransProfile.Key1 = BaiduTranslateAppId ?? "";
            baiduTransProfile.Key2 = BaiduTranslateSecretKey ?? "";

            // Auto-set as active profiles if not already set
            if (string.IsNullOrEmpty(Config.OcrProfileId))
            {
                Config.OcrProfileId = baiduOcrProfile.Id;
            }
            if (string.IsNullOrEmpty(Config.TranslateProfileId))
            {
                Config.TranslateProfileId = baiduTransProfile.Id;
            }

            _settingsService.SaveConfig();

            if (_mainViewModel?.ExternalToolsVM != null)
            {
                _mainViewModel.ExternalToolsVM.RefreshProfiles();
            }
        }

        /// <summary>
        /// 加载腾讯云凭据
        /// </summary>
        private void LoadTencentCredentials()
        {
            var tencentOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);
            var tencentTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

            // 加载 OCR 凭据
            if (tencentOcrProfile != null)
            {
                TencentOcrSecretId = tencentOcrProfile.Key1;
                TencentOcrSecretKey = tencentOcrProfile.Key2;
            }

            // 加载翻译凭据
            if (tencentTransProfile != null)
            {
                TencentTranslateSecretId = tencentTransProfile.Key1;
                TencentTranslateSecretKey = tencentTransProfile.Key2;
            }
        }

        /// <summary>
        /// 保存腾讯云凭据
        /// </summary>
        private void SaveTencentCredentials()
        {
            // OCR Profile
            var tencentOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);

            if (tencentOcrProfile == null)
            {
                tencentOcrProfile = new ApiProfile
                {
                    Name = "腾讯云 OCR",
                    Provider = ApiProvider.Tencent,
                    ServiceType = ServiceType.OCR
                };
                Config.ApiProfiles.Add(tencentOcrProfile);
            }

            tencentOcrProfile.Key1 = TencentOcrSecretId ?? "";
            tencentOcrProfile.Key2 = TencentOcrSecretKey ?? "";

            // Translation Profile
            var tencentTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

            if (tencentTransProfile == null)
            {
                tencentTransProfile = new ApiProfile
                {
                    Name = "腾讯云翻译",
                    Provider = ApiProvider.Tencent,
                    ServiceType = ServiceType.Translation
                };
                Config.ApiProfiles.Add(tencentTransProfile);
            }

            tencentTransProfile.Key1 = TencentTranslateSecretId ?? "";
            tencentTransProfile.Key2 = TencentTranslateSecretKey ?? "";

            // Auto-set as active profiles if undefined
            if (string.IsNullOrEmpty(Config.OcrProfileId))
            {
                Config.OcrProfileId = tencentOcrProfile.Id;
            }
            if (string.IsNullOrEmpty(Config.TranslateProfileId))
            {
                Config.TranslateProfileId = tencentTransProfile.Id;
            }

            _settingsService.SaveConfig();

            if (_mainViewModel?.ExternalToolsVM != null)
            {
                _mainViewModel.ExternalToolsVM.RefreshProfiles();
            }
        }

        /// <summary>
        /// 加载 Google 凭据
        /// </summary>
        private void LoadGoogleCredentials()
        {
            var googleProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);

            if (googleProfile != null)
            {
                GoogleBaseUrl = googleProfile.BaseUrl;
                GoogleApiKey = googleProfile.Key1;
            }
        }

        /// <summary>
        /// 保存 Google 凭据
        /// </summary>
        private void SaveGoogleCredentials()
        {
            var googleProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);

            if (googleProfile == null)
            {
                googleProfile = new ApiProfile
                {
                    Name = "Google 翻译",
                    Provider = ApiProvider.Google,
                    ServiceType = ServiceType.Translation
                };
                Config.ApiProfiles.Add(googleProfile);
            }

            googleProfile.BaseUrl = GoogleBaseUrl ?? "";
            googleProfile.Key1 = GoogleApiKey ?? "";

            _settingsService.SaveConfig();

            if (_mainViewModel?.ExternalToolsVM != null)
            {
                _mainViewModel.ExternalToolsVM.RefreshProfiles();
            }
        }

        /// <summary>
        /// 加载有道凭据
        /// </summary>
        private void LoadYoudaoCredentials()
        {
            var youdaoOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.OCR);
            var youdaoTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.Translation);

            // 加载 OCR 凭据
            if (youdaoOcrProfile != null)
            {
                YoudaoOcrAppKey = youdaoOcrProfile.Key1;
                YoudaoOcrAppSecret = youdaoOcrProfile.Key2;
            }

            // 加载翻译凭据
            if (youdaoTransProfile != null)
            {
                YoudaoTranslateAppKey = youdaoTransProfile.Key1;
                YoudaoTranslateAppSecret = youdaoTransProfile.Key2;
            }
        }

        /// <summary>
        /// 保存有道凭据
        /// </summary>
        private void SaveYoudaoCredentials()
        {
            // OCR Profile
            var youdaoOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.OCR);

            if (youdaoOcrProfile == null)
            {
                youdaoOcrProfile = new ApiProfile
                {
                    Name = "有道 OCR",
                    Provider = ApiProvider.Youdao,
                    ServiceType = ServiceType.OCR
                };
                Config.ApiProfiles.Add(youdaoOcrProfile);
            }

            youdaoOcrProfile.Key1 = YoudaoOcrAppKey ?? "";
            youdaoOcrProfile.Key2 = YoudaoOcrAppSecret ?? "";

            // Translation Profile
            var youdaoTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.Translation);

            if (youdaoTransProfile == null)
            {
                youdaoTransProfile = new ApiProfile
                {
                    Name = "有道翻译",
                    Provider = ApiProvider.Youdao,
                    ServiceType = ServiceType.Translation
                };
                Config.ApiProfiles.Add(youdaoTransProfile);
            }

            youdaoTransProfile.Key1 = YoudaoTranslateAppKey ?? "";
            youdaoTransProfile.Key2 = YoudaoTranslateAppSecret ?? "";

            _settingsService.SaveConfig();

            if (_mainViewModel?.ExternalToolsVM != null)
            {
                _mainViewModel.ExternalToolsVM.RefreshProfiles();
            }
        }

        #endregion

        #region Commands - Launch Bar

        [RelayCommand]
        private void AddLaunchBarItem()
        {
            Config.LaunchBarItems.Add(new LaunchBarItem
            {
                ColorHex = "#FF007ACC",
                ActionType = LaunchBarActionType.BuiltIn,
                ActionTarget = "ToggleWindow",
                Label = "主界面"
            });
            _settingsService.SaveConfig();
        }

        [RelayCommand]
        private void RemoveLaunchBarItem(LaunchBarItem? item)
        {
            if (item != null)
            {
                Config.LaunchBarItems.Remove(item);
                _settingsService.SaveConfig();
            }
        }

        [RelayCommand]
        private void MoveLaunchBarItemUp(LaunchBarItem? item)
        {
            if (item != null)
            {
                int index = Config.LaunchBarItems.IndexOf(item);
                if (index > 0)
                {
                    Config.LaunchBarItems.Move(index, index - 1);
                    _settingsService.SaveConfig();
                }
            }
        }

        [RelayCommand]
        private void MoveLaunchBarItemDown(LaunchBarItem? item)
        {
            if (item != null)
            {
                int index = Config.LaunchBarItems.IndexOf(item);
                if (index >= 0 && index < Config.LaunchBarItems.Count - 1)
                {
                    Config.LaunchBarItems.Move(index, index + 1);
                    _settingsService.SaveConfig();
                }
            }
        }

        #endregion
    }
}
