using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using PromptMasterv6.Features.Main.Messages;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Features.Main;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace PromptMasterv6.Features.Settings
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IAiService _aiService;
        private readonly IDataService _dataService;
        private readonly FileDataService _localDataService;
        private readonly IDialogService _dialogService;
        private readonly IHotkeyService _hotkeyService;
        private readonly IWindowManager _windowManager;

        private MainViewModel? _mainViewModel;

        public void SetMainViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            SyncVM.SetMainViewModel(mainViewModel);
        }

        #region Child ViewModels

        public AiModelsViewModel AiModelsVM { get; }
        public SyncViewModel SyncVM { get; }
        public LauncherSettingsViewModel LauncherSettingsVM { get; }
        public ApiCredentialsViewModel ApiCredentialsVM { get; }
        
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

        #endregion

        #region Observable Properties - Sync & Restore

        [ObservableProperty] private bool isRestoreConfirmVisible;
        [ObservableProperty] private string? restoreStatus;
        [ObservableProperty] private System.Windows.Media.Brush restoreStatusColor = System.Windows.Media.Brushes.Green;

        #endregion

        #region Observable Properties - AI Translation Test

        [ObservableProperty] private string? translationTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush translationTestStatusColor = System.Windows.Media.Brushes.Gray;

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
            IDialogService dialogService,
            IHotkeyService hotkeyService,
            IWindowManager windowManager,
            AiModelsViewModel aiModelsVM,
            SyncViewModel syncVM,
            LauncherSettingsViewModel launcherSettingsVM,
            ApiCredentialsViewModel apiCredentialsVM)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _dataService = dataService;
            _localDataService = localDataService;
            _dialogService = dialogService;
            _hotkeyService = hotkeyService;
            _windowManager = windowManager;

            AiModelsVM = aiModelsVM;
            SyncVM = syncVM;
            LauncherSettingsVM = launcherSettingsVM;
            ApiCredentialsVM = apiCredentialsVM;

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
                        
                        ApiCredentialsVM.LoadAllCredentials();
                        
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
