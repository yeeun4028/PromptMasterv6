using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.AiModels;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Workspace;
using PromptMasterv6.Features.AiModels.TestConnection;
using PromptMasterv6.Features.AiModels.DeleteModel;
using PromptMasterv6.Features.AiModels.AddModel;
using PromptMasterv6.Features.AiModels.RenameModel;
using PromptMasterv6.Features.AiModels.TestTranslationBatch;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    public class SettingsServiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<SettingsViewModel>();
            
            services.AddTransient<AddAiModelViewModel>();
            services.AddTransient<DeleteAiModelViewModel>();
            services.AddTransient<RenameAiModelViewModel>();
            services.AddTransient<TestAiConnectionViewModel>();
            services.AddTransient<AiModelsViewModel>();
            
            services.AddSingleton<SyncViewModel>();
            services.AddSingleton<LauncherSettingsViewModel>();
            services.AddSingleton<ApiCredentialsViewModel>();
            services.AddSingleton<Features.Settings.Shortcut.ShortcutViewModel>();
            services.AddSingleton<Features.Settings.Automation.AutomationViewModel>();
            services.AddSingleton<Features.Settings.Window.WindowViewModel>();
            services.AddSingleton<Features.Settings.Proxy.ProxyViewModel>();
            services.AddSingleton<Features.Settings.LaunchBar.LaunchBarViewModel>();
            services.AddSingleton<Features.Settings.ExternalTools.ExternalToolsSettingsViewModel>();

            services.AddTransient<SettingsView>();
            services.AddTransient<Features.Settings.Shortcut.ShortcutView>();
            services.AddTransient<Features.Settings.Launcher.LauncherView>();
            services.AddTransient<Features.Settings.Window.WindowView>();
            services.AddTransient<Features.Settings.Automation.AutomationView>();
            services.AddTransient<AiModelsView>();
            services.AddTransient<SyncView>();
            services.AddTransient<Features.Settings.ExternalTools.ExternalToolsSettingsView>();
            services.AddTransient<Features.Settings.LaunchBar.LaunchBarView>();
            services.AddTransient<Features.Settings.Proxy.ProxyView>();

            services.AddTransient<LauncherViewModel>();
            services.AddTransient<WorkspaceViewModel>();
            services.AddTransient<LauncherWindow>();
            services.AddTransient<SettingsWindow>();

            services.AddSingleton<SelectSettingsTabFeature.Handler>();
            services.AddSingleton<CloseSettingsFeature.Handler>();

            services.AddSingleton<Features.AiModels.TestConnection.TestAiConnectionFeature.Handler>();
            services.AddSingleton<Features.AiModels.DeleteModel.DeleteAiModelFeature.Handler>();
            services.AddSingleton<Features.AiModels.AddModel.AddAiModelFeature.Handler>();
            services.AddSingleton<Features.AiModels.RenameModel.RenameAiModelFeature.Handler>();
            services.AddSingleton<Features.AiModels.TestTranslationBatch.TestAiTranslationBatchFeature.Handler>();

            services.AddSingleton<Features.Settings.Launcher.AddSearchPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Launcher.RemoveSearchPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Launcher.SelectSearchPathFeature.Handler>();

            services.AddSingleton<Features.Settings.Sync.GetBackupListFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ManualRestoreFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ManualLocalRestoreFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ManualBackupFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ExportConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ImportConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.SelectExportPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.SelectImportPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.OpenLogFolder.OpenLogFolderFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ClearLogs.ClearLogsFeature.Handler>();

            services.AddSingleton<Features.Settings.ExternalTools.SaveAiTranslationConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.ExternalTools.DeleteAiTranslationConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.ExternalTools.HandleAiModelDeleted.HandleAiModelDeletedFeature.Handler>();
            services.AddSingleton<Features.Settings.ExternalTools.SelectSubTabFeature.Handler>();

            services.AddSingleton<Features.Settings.LaunchBar.AddLaunchBarItemFeature.Handler>();
            services.AddSingleton<Features.Settings.LaunchBar.RemoveLaunchBarItemFeature.Handler>();
            services.AddSingleton<Features.Settings.LaunchBar.MoveLaunchBarItemFeature.Handler>();

            services.AddSingleton<Features.Settings.ApiCredentials.TestBaiduOcrFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestBaiduTranslateFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestTencentOcrFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestTencentTranslateFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestGoogleFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.SaveApiCredentialsFeature.Handler>();

            services.AddSingleton<Features.Settings.Proxy.UpdateProxyFeature.Handler>();

            services.AddSingleton<Features.Settings.Window.UpdateWindowSettingsFeature.Handler>();

            services.AddSingleton<Features.Settings.Automation.UpdateAutomationFeature.Handler>();

            services.AddSingleton<Features.Settings.Shortcut.UpdateShortcutFeature.Handler>();

            services.AddSingleton<Features.Workspace.LoadWorkspaceData.LoadWorkspaceDataFeature.Handler>();
            services.AddSingleton<Features.Workspace.SearchOnGitHub.SearchOnGitHubFeature.Handler>();
            services.AddSingleton<Features.Workspace.ChangeFileIcon.ChangeFileIconFeature.Handler>();
            services.AddSingleton<Features.Workspace.DeleteFile.DeleteFileFeature.Handler>();
            services.AddSingleton<Features.Workspace.SyncVariables.SyncVariablesFeature.Handler>();
            services.AddSingleton<Features.Workspace.ToggleEditMode.ToggleEditModeFeature.Handler>();
            services.AddSingleton<Features.Workspace.SendToWebTarget.SendToWebTargetFeature.Handler>();
            services.AddSingleton<Features.Workspace.FilterFiles.FilterFilesFeature.Handler>();

            services.AddSingleton<Features.Launcher.ReorderLauncherItems.ReorderLauncherItemsFeature.Handler>();
            services.AddSingleton<Features.Launcher.FilterLauncherItems.FilterLauncherItemsFeature.Handler>();
            services.AddSingleton<Features.Launcher.InitializeLauncher.InitializeLauncherFeature.Handler>();
            services.AddSingleton<Features.Launcher.SwitchLauncherCategory.SwitchLauncherCategoryFeature.Handler>();
        }
    }
}
