using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using PromptMasterv6.Features.Main.Messages;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly AiService _aiService;
        private readonly IDataService _dataService;
        private readonly FileDataService _localDataService;
        private readonly DialogService _dialogService;
        private readonly HotkeyService _hotkeyService;
        private readonly WindowManager _windowManager;
        private readonly LoggerService _logger;
        private readonly ISessionState _sessionState;

        #region Child ViewModels

        public AiModelsViewModel AiModelsVM { get; }
        public SyncViewModel SyncVM { get; }
        public LauncherSettingsViewModel LauncherSettingsVM { get; }
        public ApiCredentialsViewModel ApiCredentialsVM { get; }
        public ExternalTools.ExternalToolsViewModel ExternalToolsVM { get; }
        
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

        #region Observable Properties - Sync & Restore

        [ObservableProperty] private bool isRestoreConfirmVisible;
        [ObservableProperty] private string? restoreStatus;
        [ObservableProperty] private System.Windows.Media.Brush restoreStatusColor = System.Windows.Media.Brushes.Green;

        #endregion

        #region Configuration Access

        public AppConfig Config => _settingsService.Config;
        public LocalSettings LocalConfig => _settingsService.LocalConfig;

        #endregion

        public SettingsViewModel(
            SettingsService settingsService,
            AiService aiService,
            [FromKeyedServices("cloud")] IDataService dataService,
            FileDataService localDataService,
            DialogService dialogService,
            HotkeyService hotkeyService,
            WindowManager windowManager,
            LoggerService logger,
            AiModelsViewModel aiModelsVM,
            SyncViewModel syncVM,
            LauncherSettingsViewModel launcherSettingsVM,
            ApiCredentialsViewModel apiCredentialsVM,
            ExternalTools.ExternalToolsViewModel externalToolsVM,
            ISessionState sessionState)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _dataService = dataService;
            _localDataService = localDataService;
            _dialogService = dialogService;
            _hotkeyService = hotkeyService;
            _windowManager = windowManager;
            _logger = logger;
            _sessionState = sessionState;

            AiModelsVM = aiModelsVM;
            SyncVM = syncVM;
            LauncherSettingsVM = launcherSettingsVM;
            ApiCredentialsVM = apiCredentialsVM;
            ExternalToolsVM = externalToolsVM;

            _logger.LogInfo("SettingsViewModel initialized", "SettingsViewModel.ctor");
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
                _logger.LogException(ex, "Failed to add launcher search path", "SettingsViewModel.AddLauncherSearchPath");
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

                _logger.LogInfo($"Restored {data.Folders?.Count ?? 0} folders and {data.Files?.Count ?? 0} files", "SettingsViewModel.ManualRestore");
            }
            catch (Exception ex)
            {
                RestoreStatus = $"❌ 恢复失败: {ex.Message}";
                RestoreStatusColor = System.Windows.Media.Brushes.Red;
                _logger.LogException(ex, "Failed to restore from cloud", "SettingsViewModel.ManualRestore");
            }
        }

        private void ApplyRestoredData(AppData data)
        {
            _sessionState.Files.Clear();
            _sessionState.Folders.Clear();

            if (data.Folders != null && data.Folders.Count > 0)
            {
                foreach (var folder in data.Folders)
                {
                    _sessionState.Folders.Add(folder);
                }
                _sessionState.SelectedFolder = _sessionState.Folders.FirstOrDefault();
            }
            else
            {
                var defaultFolder = new FolderItem { Name = "默认" };
                _sessionState.Folders.Add(defaultFolder);
                _sessionState.SelectedFolder = defaultFolder;
            }

            if (data.Files != null && data.Files.Count > 0)
            {
                foreach (var file in data.Files)
                {
                    if (string.IsNullOrWhiteSpace(file.FolderId) && _sessionState.SelectedFolder != null)
                    {
                        file.FolderId = _sessionState.SelectedFolder.Id;
                    }
                    _sessionState.Files.Add(file);
                }
            }

            _sessionState.RefreshFilesView();
            WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
        }

        [RelayCommand]
        private async Task ManualLocalRestore()
        {
            var service = _localDataService as FileDataService;
            if (service == null) return;

            var backups = service.GetBackups();
            _logger.LogInfo($"Found {backups.Count} backups in {service.BackupDirectory}", "SettingsViewModel.ManualLocalRestore");

            if (backups.Count == 0)
            {
                _dialogService.ShowToast($"在以下路径未找到本地备份文件：\n{service.BackupDirectory}\n\n请确保已进行过保存操作。", "Warning");
                return;
            }

            var dialog = new BackupSelectionDialog(backups);
            
            var activeWindow = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
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
            try
            {
                await _dataService.SaveAsync(_sessionState.Folders, _sessionState.Files);
                _sessionState.LocalConfig.LastCloudSyncTime = DateTime.Now;
                _sessionState.IsDirty = false;
                _sessionState.IsEditMode = false;
                _settingsService.SaveLocalConfig();
                _logger.LogInfo("Manual cloud backup successful", "SettingsViewModel.ManualBackup");
                _dialogService.ShowToast("云端备份成功！", "Success");
            }
            catch (Exception ex)
            {
                _dialogService.ShowAlert($"备份失败: {ex.Message}", "错误");
                _logger.LogException(ex, "Failed to manual backup", "SettingsViewModel.ManualBackup");
            }
        }

        #endregion

        #region Commands - Log Management

        [RelayCommand]
        private void OpenLogFolder()
        {
            try
            {
                var logPath = _logger.GetLogDirectory();
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
                _logger.LogException(ex, "Failed to open log folder", "SettingsViewModel.OpenLogFolder");
                _dialogService.ShowAlert($"无法打开日志文件夹: {ex.Message}", "错误");
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            try
            {
                _logger.ClearLogs();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to clear logs", "SettingsViewModel.ClearLogs");
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
