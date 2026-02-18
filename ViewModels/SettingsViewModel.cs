using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NHotkey;
using NHotkey.Wpf;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace PromptMasterv5.ViewModels
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
        private readonly IDataService _localDataService;
        private readonly GlobalKeyService _keyService;

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
        
        [ObservableProperty] private AiModelConfig? selectedSavedModel;

        partial void OnSelectedSavedModelChanged(AiModelConfig? value)
        {
            if (value != null)
            {
                // Sync fields (do NOT overwrite if they are somehow bound to something else, but here they are simple strings)
                Config.AiBaseUrl = value.BaseUrl;
                Config.AiApiKey = value.ApiKey;
                Config.AiModel = value.ModelName;
                
                // Also set active model ID if that's the desired behavior (it is currently)
                ActivateAiModelCommand.Execute(value);
            }
        }

        #endregion

        #region Observable Properties - Sync & Restore

        [ObservableProperty] private bool isRestoreConfirmVisible;
        [ObservableProperty] private string? restoreStatus;
        [ObservableProperty] private System.Windows.Media.Brush restoreStatusColor = System.Windows.Media.Brushes.Green;

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
            GlobalKeyService keyService)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _dataService = dataService;
            _localDataService = localDataService;
            _keyService = keyService;

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
        }

        [RelayCommand]
        private void ToggleNavigation()
        {
            IsNavigationVisible = !IsNavigationVisible;
        }

        #endregion

        #region Commands - Theme Management

        [RelayCommand]
        private void ToggleTheme()
        {
            LocalConfig.Theme = LocalConfig.Theme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark;
            ApplyTheme();
            _settingsService.SaveLocalConfig();
        }

        public void ApplyTheme()
        {
            var app = System.Windows.Application.Current;
            if (app?.Resources == null) return;

            ApplyTheme(LocalConfig.Theme);
        }

        private void ApplyTheme(ThemeType theme)
        {
            var resources = System.Windows.Application.Current?.Resources;
            if (resources == null) return;

            static void SetBrush(ResourceDictionary res, string key, string color)
            {
                static System.Windows.Media.Color ParseColor(string value) =>
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
                res[key] = new System.Windows.Media.SolidColorBrush(ParseColor(color));
            }

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
                SetBrush(resources, "InputTextBrush", "#DEDEDE");
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
                SetBrush(resources, "InputTextBrush", "#666666");
            }
        }

        #endregion

        #region Commands - AI Model Management

        public Task<(bool Success, string Message)> TestAiConnectionAsync() =>
            _aiService.TestConnectionAsync(Config);

        public async Task<(bool Success, string Message)> TestAiTranslationConnectionAsync()
        {
            var enabledModels = Config.SavedModels.Where(m => m.IsEnableForTranslation).ToList();
            if (enabledModels.Count == 0)
            {
                return (false, "请先勾选至少一个参与翻译的 AI 模型");
            }

            int successCount = 0;
            string lastError = "";

            foreach (var model in enabledModels)
            {
                var result = await _aiService.TestConnectionAsync(model.ApiKey, model.BaseUrl, model.ModelName);
                if (result.Success) successCount++;
                else lastError = result.Message;
            }

            if (successCount == enabledModels.Count)
                return (true, $"全部 {successCount} 个模型连接成功");
            else if (successCount > 0)
                return (true, $"部分成功 ({successCount}/{enabledModels.Count})\n失败示例: {lastError}");
            else
                return (false, $"全部失败。\n错误示例: {lastError}");
        }

        [RelayCommand]
        private void ActivateAiModel(AiModelConfig? model)
        {
            if (model == null) return;
            Config.ActiveModelId = model.Id;
            _settingsService.SaveConfig();
        }

        [RelayCommand]
        private void AddAiModel()
        {
            if (string.IsNullOrWhiteSpace(Config.AiModel))
            {
                MessageBox.Show("请输入模型名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newModel = new AiModelConfig
            {
                Id = Guid.NewGuid().ToString(),
                ModelName = Config.AiModel,
                BaseUrl = Config.AiBaseUrl,
                ApiKey = Config.AiApiKey
            };

            Config.SavedModels.Insert(0, newModel);
            Config.ActiveModelId = newModel.Id;
            _settingsService.SaveConfig();
        }

        [RelayCommand]
        private void DeleteAiModel(AiModelConfig? model)
        {
            if (model == null) return;
            var idx = Config.SavedModels.IndexOf(model);
            if (idx >= 0) Config.SavedModels.RemoveAt(idx);
            if (Config.ActiveModelId == model.Id) Config.ActiveModelId = "";
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
                HotkeyManager.Current.Remove("ToggleWindow");
                HotkeyManager.Current.Remove("ToggleWindowSingle");

                // Register Full and Mini window hotkeys using helper method
                RegisterWindowHotkey("ToggleFullWindowHotkey", Config.FullWindowHotkey, () => _mainViewModel?.OnWindowHotkeyPressed());
                RegisterWindowHotkey("ToggleMiniWindowHotkey", Config.MiniWindowHotkey, () => _mainViewModel?.OnWindowHotkeyPressed());
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
                try { HotkeyManager.Current.Remove("ScreenshotTranslate"); } catch { }
                try { HotkeyManager.Current.Remove("SelectedTextTranslate"); } catch { }
                try { HotkeyManager.Current.Remove("OcrOnly"); } catch { }
                try { HotkeyManager.Current.Remove("GlobalQuickAction"); } catch { }

                // Register new hotkeys from external tools settings
                RegisterWindowHotkey("ScreenshotTranslate", Config.ScreenshotTranslateHotkey, () => _mainViewModel.ExternalToolsVM.TriggerTranslateCommand.Execute(null));
                RegisterWindowHotkey("SelectedTextTranslate", Config.SelectedTextTranslateHotkey, () => _mainViewModel.ExternalToolsVM.TriggerSelectedTextTranslateCommand.Execute(null));
                RegisterWindowHotkey("OcrOnly", Config.OcrHotkey, () => _mainViewModel.ExternalToolsVM.TriggerOcrCommand.Execute(null));
                RegisterWindowHotkey("GlobalQuickAction", Config.QuickActionHotkey, () => _mainViewModel.TriggerQuickActionCommand.Execute(null));
                
                // Update Launcher Hotkey
                UpdateLauncherHotkey();

                _settingsService.SaveConfig();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to update external tools hotkeys", "SettingsViewModel.UpdateExternalToolsHotkeys");
            }
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

        public void UpdateLauncherHotkey()
        {
            try
            {
                _keyService.LauncherHotkeyString = Config.LauncherHotkey;
                _settingsService.SaveConfig();
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
                System.Windows.MessageBox.Show($"添加文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("系统初始化未完成", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _mainViewModel.SyncMiniPinnedPrompts();

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
                MessageBox.Show("系统初始化未完成", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 1. Get backups
            var service = _localDataService as FileDataService;
            if (service == null) return;

            var backups = service.GetBackups();
            LoggerService.Instance.LogInfo($"Found {backups.Count} backups in {service.BackupDirectory}", "SettingsViewModel.ManualLocalRestore");

            if (backups.Count == 0)
            {
                MessageBox.Show($"在以下路径未找到本地备份文件：\n{service.BackupDirectory}\n\n请确保已进行过保存操作。", "未找到备份", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. Show dialog
            var dialog = new Views.BackupSelectionDialog(backups);
            if (dialog.ShowDialog() != true || dialog.SelectedBackup == null) return;

            // 3. Confirm
            var selected = dialog.SelectedBackup;
            if (MessageBox.Show($"确定要恢复到备份点：\n{selected.DisplayText} 吗？\n当前未保存的更改将会丢失。", 
                "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
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
                _mainViewModel.SyncMiniPinnedPrompts();

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
                MessageBox.Show("系统初始化未完成", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"备份失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("日志文件夹不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to open log folder", "SettingsViewModel.OpenLogFolder");
                MessageBox.Show($"无法打开日志文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            var result = MessageBox.Show(
                "确定要清除所有日志文件吗？此操作不可撤销。",
                "确认清除日志",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    LoggerService.Instance.ClearLogs();
                    MessageBox.Show("日志已清除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogException(ex, "Failed to clear logs", "SettingsViewModel.ClearLogs");
                    MessageBox.Show($"清除日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
                FileName = $"PromptMaster_Config_{DateTime.Now:yyyyMMdd_HHmm}.zip"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _settingsService.ExportSettings(dialog.FileName);
                    MessageBox.Show("配置导出成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"配置导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

            if (dialog.ShowDialog() == true)
            {
                if (MessageBox.Show("导入配置将覆盖当前的设置，确定要继续吗？\n(操作后将自动重启生效)",
                    "确认导入", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _settingsService.ImportSettings(dialog.FileName);
                        
                        // 尝试重新应用设置
                        ApplyTheme();
                        UpdateWindowHotkeys();
                        UpdateExternalToolsHotkeys();
                        
                        // 由于配置可能发生彻底变化，建议用户重启或重新初始化一些状态
                        // 这里我们刷新一下当前 ViewState
                        OnPropertyChanged(nameof(Config));
                        OnPropertyChanged(nameof(LocalConfig));

                        MessageBox.Show("配置导入成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"配置导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion
    }
}
