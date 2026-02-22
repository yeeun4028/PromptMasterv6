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
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IDialogService _dialogService;

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
            IDialogService dialogService)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _dataService = dataService;
            _localDataService = localDataService;
            _keyService = keyService;
            _dialogService = dialogService;

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

            foreach (var model in enabledModels)
            {
                var result = await _aiService.TestConnectionAsync(model.ApiKey, model.BaseUrl, model.ModelName);
                if (result.Success) successCount++;
                else lastError = result.Message;
            }

            if (successCount == enabledModels.Count)
            {
                TranslationTestStatus = $"全部 {successCount} 个模型连接成功";
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
                 // Fallback to config if no specific model passed (though UI now passes parameter)
                 var (success, msg) = await _aiService.TestConnectionAsync(Config);
                 TestStatus = msg;
                 TestStatusColor = success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                 return;
            }

            TestStatus = "测试中...";
            TestStatusColor = System.Windows.Media.Brushes.Gray;
            var (success2, message) = await _aiService.TestConnectionAsync(model.ApiKey, model.BaseUrl, model.ModelName);
            TestStatus = message;
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

        #region Commands - Voice Control

        [RelayCommand]
        private void OpenVoiceCommandsConfig()
        {
            try
            {
                // Ensure the file exists
                string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Config.VoiceCommandConfigPath);
                if (!System.IO.File.Exists(configPath))
                {
                    // Create default file if not exists (although AppConfig should handle it, double check)
                    System.IO.File.WriteAllText(configPath, 
                        "{\n  \"打开计算器\": \"calc.exe\",\n  \"打开记事本\": \"notepad.exe\"\n}");
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = configPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowAlert($"无法打开配置文件: {ex.Message}", "错误");
                LoggerService.Instance.LogException(ex, "Failed to open voice command config", "SettingsViewModel.OpenVoiceCommandsConfig");
            }
        }
        
        public void UpdateVoiceTriggerHotkey()
        {
            try
            {
                 // Re-register hotkey in GlobalKeyService via MainViewModel or direct service access if available
                 // For now, we just save config, GlobalKeyService should listen to config changes or be manually updated
                 // Since we don't have direct access to GlobalKeyService's registration method from here easily without exposing it,
                 // we rely on the fact that GlobalKeyService might reload on config save or we need to add a method there.
                 // Actually we have _keyService injected. Let's use it if possible, or just save config.
                 // In the plan, we said GlobalKeyService would use AppConfig.
                 
                _settingsService.SaveConfig();
                
                 // Trigger re-registration in GlobalKeyService
                 _keyService.UpdateVoiceHotkey(Config.VoiceTriggerHotkey);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to update voice trigger hotkey", "SettingsViewModel.UpdateVoiceTriggerHotkey");
            }
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
                _dialogService.ShowAlert("系统初始化未完成", "错误");
                return;
            }

            try
            {
                var voiceCommandService = (System.Windows.Application.Current as App)?.ServiceProvider.GetRequiredService<ICommandExecutionService>();
                var voiceCommands = voiceCommandService?.GetCommands() ?? new Dictionary<string, string>();
                
                await _dataService.SaveAsync(_mainViewModel.SidebarVM.Folders, _mainViewModel.Files, voiceCommands);
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
                FileName = $"PromptMaster_Config_{DateTime.Now:yyyyMMdd_HHmm}.zip"
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
                        
                        // 尝试重新应用设置
                        ApplyTheme();
                        UpdateWindowHotkeys();
                        UpdateExternalToolsHotkeys();
                        
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
    }
}
