using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Workspace;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    /// <summary>
    /// 设置服务模块
    /// 注册设置相关的ViewModel、窗口和Feature Handlers
    /// </summary>
    public class SettingsServiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            // ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddSingleton<AiModelsViewModel>();
            services.AddSingleton<SyncViewModel>();
            services.AddSingleton<LauncherSettingsViewModel>();
            services.AddSingleton<ApiCredentialsViewModel>();
            services.AddSingleton<Features.Settings.Shortcut.ShortcutViewModel>();
            services.AddSingleton<Features.Settings.Automation.AutomationViewModel>();
            services.AddSingleton<Features.Settings.Window.WindowViewModel>();
            services.AddSingleton<Features.Settings.Proxy.ProxyViewModel>();
            services.AddSingleton<Features.Settings.LaunchBar.LaunchBarViewModel>();
            services.AddSingleton<Features.Settings.ExternalTools.ExternalToolsSettingsViewModel>();

            // Windows
            services.AddTransient<LauncherViewModel>();
            services.AddTransient<WorkspaceViewModel>();
            services.AddTransient<LauncherWindow>();
            services.AddTransient<SettingsWindow>();

            // Settings Features - AiModels
            services.AddSingleton<Features.Settings.AiModels.TestAiConnectionFeature.Handler>();
            services.AddSingleton<Features.Settings.AiModels.DeleteAiModelFeature.Handler>();

            // Settings Features - Launcher
            services.AddSingleton<Features.Settings.Launcher.AddSearchPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Launcher.RemoveSearchPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Launcher.SelectSearchPathFeature.Handler>();

            // Settings Features - Sync
            services.AddSingleton<Features.Settings.Sync.ManualRestoreFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ManualLocalRestoreFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ManualBackupFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ExportConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ImportConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.SelectExportPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.SelectImportPathFeature.Handler>();

            // Settings Features - ExternalTools
            services.AddSingleton<Features.Settings.ExternalTools.SaveAiTranslationConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.ExternalTools.DeleteAiTranslationConfigFeature.Handler>();

            // Settings Features - LaunchBar
            services.AddSingleton<Features.Settings.LaunchBar.AddLaunchBarItemFeature.Handler>();
            services.AddSingleton<Features.Settings.LaunchBar.RemoveLaunchBarItemFeature.Handler>();
            services.AddSingleton<Features.Settings.LaunchBar.MoveLaunchBarItemFeature.Handler>();

            // Settings Features - ApiCredentials
            services.AddSingleton<Features.Settings.ApiCredentials.TestBaiduOcrFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestBaiduTranslateFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestTencentOcrFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestTencentTranslateFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestGoogleFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.SaveApiCredentialsFeature.Handler>();

            // Settings Features - Proxy
            services.AddSingleton<Features.Settings.Proxy.UpdateProxyFeature.Handler>();

            // Settings Features - Window
            services.AddSingleton<Features.Settings.Window.UpdateWindowSettingsFeature.Handler>();

            // Settings Features - Automation
            services.AddSingleton<Features.Settings.Automation.UpdateAutomationFeature.Handler>();

            // Settings Features - Shortcut
            services.AddSingleton<Features.Settings.Shortcut.UpdateShortcutFeature.Handler>();

            // Workspace Features
            services.AddSingleton<Features.Workspace.LoadWorkspaceData.LoadWorkspaceDataFeature.Handler>();
            services.AddSingleton<Features.Workspace.SearchOnGitHub.SearchOnGitHubFeature.Handler>();
            services.AddSingleton<Features.Workspace.ChangeFileIcon.ChangeFileIconFeature.Handler>();
            services.AddSingleton<Features.Workspace.DeleteFile.DeleteFileFeature.Handler>();

            // Launcher Features
            services.AddSingleton<Features.Launcher.ReorderLauncherItems.ReorderLauncherItemsFeature.Handler>();
            services.AddSingleton<Features.Launcher.FilterLauncherItems.FilterLauncherItemsFeature.Handler>();
        }
    }
}
