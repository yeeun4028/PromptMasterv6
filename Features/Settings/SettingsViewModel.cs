using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Services;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using PromptMasterv6.Features.Main.Messages;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.ApiProviders;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
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

namespace PromptMasterv6.Features.Settings
{
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

        private MainViewModel? _mainViewModel;

        public void SetMainViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            SyncVM.SetMainViewModel(mainViewModel);
        }

        #region Child ViewModels

        public AiModelsViewModel AiModelsVM { get; }
        public ApiProvidersViewModel ApiProvidersVM { get; }
        public SyncViewModel SyncVM { get; }
        public LauncherSettingsViewModel LauncherSettingsVM { get; }
        
        public SettingsViewModel SettingsVM => this;

        #endregion

        #region Observable Properties - UI State

        [ObservableProperty] private bool isSettingsOpen;
        [ObservableProperty] private int selectedSettingsTab;

        [RelayCommand]
        private void SelectSettingsTab(string tabIndexStr)
        {
            if (int.TryParse(tabIndexStr, out int tabIndex))
            {
                SelectedSettingsTab = tabIndex;
            }
        }

        #endregion

        #region Observable Properties - AI Model Management

        [ObservableProperty] private string? testStatus;
        [ObservableProperty] private System.Windows.Media.Brush testStatusColor = System.Windows.Media.Brushes.Gray;

        [ObservableProperty] private AiModelConfig? selectedSavedModel;

        partial void OnSelectedSavedModelChanged(AiModelConfig? value)
        {
        }

        #endregion

        #region Observable Properties - Sync & Restore

        [ObservableProperty] private bool isRestoreConfirmVisible;
        [ObservableProperty] private string? restoreStatus;
        [ObservableProperty] private System.Windows.Media.Brush restoreStatusColor = System.Windows.Media.Brushes.Green;

        #endregion

        #region Observable Properties - Baidu Credentials

        [ObservableProperty] private string? baiduOcrApiKey;
        [ObservableProperty] private string? baiduOcrSecretKey;
        [ObservableProperty] private string? baiduTranslateAppId;
        [ObservableProperty] private string? baiduTranslateSecretKey;

        #endregion

        #region Observable Properties - Tencent Credentials

        [ObservableProperty] private string? tencentOcrSecretId;
        [ObservableProperty] private string? tencentOcrSecretKey;
        [ObservableProperty] private string? tencentTranslateSecretId;
        [ObservableProperty] private string? tencentTranslateSecretKey;

        #endregion

        #region Observable Properties - Youdao Credentials

        [ObservableProperty] private string? youdaoOcrAppKey;
        [ObservableProperty] private string? youdaoOcrAppSecret;
        [ObservableProperty] private string? youdaoTranslateAppKey;
        [ObservableProperty] private string? youdaoTranslateAppSecret;

        #endregion

        #region Observable Properties - Google Credentials

        [ObservableProperty] private string? googleBaseUrl;
        [ObservableProperty] private string? googleApiKey;

        #endregion

        #region Observable Properties - OCR & Translation Test Status

        [ObservableProperty] private string? baiduOcrTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush baiduOcrTestStatusColor = System.Windows.Media.Brushes.Gray;
        [ObservableProperty] private string? baiduTranslateTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush baiduTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;
        [ObservableProperty] private string? tencentOcrTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush tencentOcrTestStatusColor = System.Windows.Media.Brushes.Gray;
        [ObservableProperty] private string? tencentTranslateTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush tencentTranslateTestStatusColor = System.Windows.Media.Brushes.Gray;
        [ObservableProperty] private string? youdaoTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush youdaoTestStatusColor = System.Windows.Media.Brushes.Gray;
        [ObservableProperty] private string? googleTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush googleTestStatusColor = System.Windows.Media.Brushes.Gray;

        #endregion

        #region Configuration Access

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
            IWindowManager windowManager,
            AiModelsViewModel aiModelsVM,
            ApiProvidersViewModel apiProvidersVM,
            SyncViewModel syncVM,
            LauncherSettingsViewModel launcherSettingsVM)
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

            AiModelsVM = aiModelsVM;
            ApiProvidersVM = apiProvidersVM;
            SyncVM = syncVM;
            LauncherSettingsVM = launcherSettingsVM;

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
            SelectedSavedModel = newModel;
            
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
            
            if (Config.ActiveModelId == model.Id) Config.ActiveModelId = "";
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
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        public void UpdateExternalToolsHotkeys()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        public void UpdateLauncherHotkey()
        {
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
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

            _settingsService.SaveConfig();

            IsRestoreConfirmVisible = false;
            RestoreStatus = "正在从云端恢复数据...";
            RestoreStatusColor = System.Windows.Media.Brushes.Orange;

            try
            {
                var data = await _dataService.LoadAsync();

                if (data == null || ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0))
                {
                    RestoreStatus = "❌ 云端没有数据可恢复 (或连接失败)";
                    RestoreStatusColor = System.Windows.Media.Brushes.Red;
                    return;
                }

                ApplyRestoredData(data);

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

        private void ApplyRestoredData(AppData data)
        {
            if (_mainViewModel == null) return;

            _mainViewModel.Files.Clear();
            _mainViewModel.Folders.Clear();

            if (data.Folders != null && data.Folders.Count > 0)
            {
                foreach (var folder in data.Folders)
                {
                    _mainViewModel.Folders.Add(folder);
                }
                _mainViewModel.SelectedFolder = _mainViewModel.Folders.FirstOrDefault();
            }
            else
            {
                var defaultFolder = new FolderItem { Name = "默认" };
                _mainViewModel.Folders.Add(defaultFolder);
                _mainViewModel.SelectedFolder = defaultFolder;
            }

            if (data.Files != null && data.Files.Count > 0)
            {
                foreach (var file in data.Files)
                {
                    if (string.IsNullOrWhiteSpace(file.FolderId) && _mainViewModel.SelectedFolder != null)
                    {
                        file.FolderId = _mainViewModel.SelectedFolder.Id;
                    }
                    _mainViewModel.Files.Add(file);
                }
            }

            _mainViewModel.UpdateFilesViewFilter();
            _mainViewModel.FilesView?.Refresh();
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        [RelayCommand]
        private async Task ManualLocalRestore()
        {
            if (_mainViewModel == null)
            {
                _dialogService.ShowAlert("系统初始化未完成", "错误");
                return;
            }

            var service = _localDataService as FileDataService;
            if (service == null) return;

            var backups = service.GetBackups();
            LoggerService.Instance.LogInfo($"Found {backups.Count} backups in {service.BackupDirectory}", "SettingsViewModel.ManualLocalRestore");

            if (backups.Count == 0)
            {
                _dialogService.ShowToast($"在以下路径未找到本地备份文件：\n{service.BackupDirectory}\n\n请确保已进行过保存操作。", "Warning");
                return;
            }

            var dialog = new BackupSelectionDialog(backups);
            
            var activeWindow = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            dialog.Owner = activeWindow ?? System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() != true || dialog.SelectedBackup == null) return;

            var selected = dialog.SelectedBackup;
            if (!_dialogService.ShowConfirmation($"确定要恢复到备份点：\n{selected.DisplayText} 吗？\n当前未保存的更改将会丢失。", "确认恢复"))
            {
                return;
            }

            RestoreStatus = "正在恢复本地数据...";
            RestoreStatusColor = System.Windows.Media.Brushes.Orange;

            try
            {
                var data = await service.RestoreLocalBackupAsync(selected.FilePath);
                if (data == null)
                {
                    throw new Exception("读取备份文件失败");
                }

                ApplyRestoredData(data);

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
                await _dataService.SaveAsync(_mainViewModel.Folders, _mainViewModel.Files);
                _mainViewModel.LocalConfig.LastCloudSyncTime = DateTime.Now;
                _mainViewModel.IsDirty = false;
                _mainViewModel.IsEditMode = false;
                _settingsService.SaveLocalConfig();
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
                        
                        LoadBaiduCredentials();
                        LoadTencentCredentials();
                        LoadYoudaoCredentials();
                        LoadGoogleCredentials();
                        
                        UpdateWindowHotkeys();
                        UpdateExternalToolsHotkeys();
                        
                        WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
                        
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

        [RelayCommand]
        private async Task TestBaiduOcr()
        {
            SaveBaiduCredentials();

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

        [RelayCommand]
        private async Task TestBaiduTranslate()
        {
            SaveBaiduCredentials();

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

        private byte[] CreateTestImage()
        {
            var width = 200;
            var height = 60;
            var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));

                var formattedText = new FormattedText(
                    "OCR TEST",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    24,
                    System.Windows.Media.Brushes.Black,
                    1.0);

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

            var msg = new RequestPromptFileMessage { PromptId = promptId };
            WeakReferenceMessenger.Default.Send(msg);
            if (msg.HasReceivedResponse && msg.Response != null && msg.Response.File != null)
            {
                WeakReferenceMessenger.Default.Send(new JumpToEditPromptMessage { File = msg.Response.File });
            }
        }

        [RelayCommand]
        private void SaveAiTranslationConfig()
        {
            var promptId = Config.AiTranslationPromptId;
            var promptTitle = "";
            if (!string.IsNullOrWhiteSpace(promptId))
            {
                var msg = new RequestPromptFileMessage { PromptId = promptId };
                WeakReferenceMessenger.Default.Send(msg);
                promptTitle = msg.HasReceivedResponse ? msg.Response?.File?.Title ?? "" : "";
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

        private void LoadBaiduCredentials()
        {
            var baiduOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);
            var baiduTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

            if (baiduOcrProfile != null)
            {
                BaiduOcrApiKey = baiduOcrProfile.Key1;
                BaiduOcrSecretKey = baiduOcrProfile.Key2;
            }

            if (baiduTransProfile != null)
            {
                BaiduTranslateAppId = baiduTransProfile.Key1;
                BaiduTranslateSecretKey = baiduTransProfile.Key2;
            }
        }

        private void SaveBaiduCredentials()
        {
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

            baiduOcrProfile.Key1 = BaiduOcrApiKey ?? "";
            baiduOcrProfile.Key2 = BaiduOcrSecretKey ?? "";

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

            baiduTransProfile.Key1 = BaiduTranslateAppId ?? "";
            baiduTransProfile.Key2 = BaiduTranslateSecretKey ?? "";

            if (string.IsNullOrEmpty(Config.OcrProfileId))
            {
                Config.OcrProfileId = baiduOcrProfile.Id;
            }
            if (string.IsNullOrEmpty(Config.TranslateProfileId))
            {
                Config.TranslateProfileId = baiduTransProfile.Id;
            }

            _settingsService.SaveConfig();

            WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
        }

        private void LoadTencentCredentials()
        {
            var tencentOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);
            var tencentTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

            if (tencentOcrProfile != null)
            {
                TencentOcrSecretId = tencentOcrProfile.Key1;
                TencentOcrSecretKey = tencentOcrProfile.Key2;
            }

            if (tencentTransProfile != null)
            {
                TencentTranslateSecretId = tencentTransProfile.Key1;
                TencentTranslateSecretKey = tencentTransProfile.Key2;
            }
        }

        private void SaveTencentCredentials()
        {
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

            if (string.IsNullOrEmpty(Config.OcrProfileId))
            {
                Config.OcrProfileId = tencentOcrProfile.Id;
            }
            if (string.IsNullOrEmpty(Config.TranslateProfileId))
            {
                Config.TranslateProfileId = tencentTransProfile.Id;
            }

            _settingsService.SaveConfig();

            WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
        }

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

            WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
        }

        private void LoadYoudaoCredentials()
        {
            var youdaoOcrProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.OCR);
            var youdaoTransProfile = Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Youdao && p.ServiceType == ServiceType.Translation);

            if (youdaoOcrProfile != null)
            {
                YoudaoOcrAppKey = youdaoOcrProfile.Key1;
                YoudaoOcrAppSecret = youdaoOcrProfile.Key2;
            }

            if (youdaoTransProfile != null)
            {
                YoudaoTranslateAppKey = youdaoTransProfile.Key1;
                YoudaoTranslateAppSecret = youdaoTransProfile.Key2;
            }
        }

        private void SaveYoudaoCredentials()
        {
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

            WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
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
