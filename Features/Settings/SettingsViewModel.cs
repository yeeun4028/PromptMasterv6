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
using PromptMasterv6.Features.Settings.LaunchBar;
using PromptMasterv6.Features.Settings.ExternalTools;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.Settings.AiModels.Messages;

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
        public global::PromptMasterv6.Features.ExternalTools.ExternalToolsViewModel ExternalToolsVM { get; }
        public LaunchBar.LaunchBarViewModel LaunchBarVM { get; }
        public ExternalToolsSettingsViewModel ExternalToolsSettingsVM { get; }
        
        public Features.Settings.Shortcut.ShortcutViewModel ShortcutVM { get; }
        public Features.Settings.Automation.AutomationViewModel AutomationVM { get; }
        public Features.Settings.Window.WindowViewModel WindowVM { get; }
        public Features.Settings.Proxy.ProxyViewModel ProxyVM { get; }
        
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

        #region Observable Properties - UI State

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
            global::PromptMasterv6.Features.ExternalTools.ExternalToolsViewModel externalToolsVM,
            LaunchBar.LaunchBarViewModel launchBarVM,
            ExternalToolsSettingsViewModel externalToolsSettingsVM,
            Features.Settings.Shortcut.ShortcutViewModel shortcutVM,
            Features.Settings.Automation.AutomationViewModel automationVM,
            Features.Settings.Window.WindowViewModel windowVM,
            Features.Settings.Proxy.ProxyViewModel proxyVM,
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
            LaunchBarVM = launchBarVM;
            ExternalToolsSettingsVM = externalToolsSettingsVM;
            ShortcutVM = shortcutVM;
            AutomationVM = automationVM;
            WindowVM = windowVM;
            ProxyVM = proxyVM;

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
    }
}
