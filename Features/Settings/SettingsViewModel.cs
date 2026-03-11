using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Features.Settings.LaunchBar;
using PromptMasterv6.Features.Settings.ExternalTools;

namespace PromptMasterv6.Features.Settings
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

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
            Features.Settings.Proxy.ProxyViewModel proxyVM)
        {
            _settingsService = settingsService;
            _logger = logger;

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
    }
}
