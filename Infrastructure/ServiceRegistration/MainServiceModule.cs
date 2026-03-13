using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Main.ManageFiles;
using PromptMasterv6.Features.Main.ContentEditor;
using PromptMasterv6.Features.Main.Backup;
using PromptMasterv6.Features.Main.Sidebar;
using PromptMasterv6.Features.Main.Tray;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    /// <summary>
    /// 主窗口服务模块
    /// 注册主窗口相关的ViewModel、窗口和Feature Handlers
    /// </summary>
    public class MainServiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            // ViewModels - Singleton for shared state across views
            services.AddSingleton<FileManagerViewModel>();
            services.AddSingleton<ContentEditorViewModel>();
            services.AddSingleton<BackupViewModel>();
            services.AddSingleton<SidebarViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<TrayViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();
            services.AddSingleton<LaunchBarWindow>();

            // Main Features - Backup
            services.AddSingleton<Features.Main.Backup.PerformCloudBackupFeature.Handler>();
            services.AddSingleton<Features.Main.Backup.PerformLocalBackupFeature.Handler>();

            // Main Features - ManageFiles
            services.AddSingleton<Features.Main.ManageFiles.ImportMarkdownFilesFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.SelectFilesForImportFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.MoveFileToFolderFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.InitializeAppDataFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.ChangeFileIconFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.CreateFolderFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.DeleteFolderFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.RenameFolderFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.ChangeFolderIconFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.CreateFileFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.DeleteFileFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.RenameFileFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.LoadAppDataFeature.Handler>();
            services.AddSingleton<Features.Main.ManageFiles.SaveAppDataFeature.Handler>();

            // Main Features - Sidebar
            services.AddSingleton<Features.Main.Sidebar.ChangeActionIconFeature.Handler>();

            // Main Features - ContentEditor
            services.AddSingleton<Features.Main.ContentEditor.SetCurrentFileFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SyncVariablesFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.ToggleEditModeFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SearchOnGitHubFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.CopyCompiledTextFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SendToWebTargetFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.OpenWebTargetFeature.Handler>();

            // Main Features - Tray
            services.AddSingleton<Features.Main.Tray.OpenSettingsFeature.Handler>();
            services.AddSingleton<Features.Main.Tray.PinToScreenFromCaptureFeature.Handler>();
            services.AddSingleton<Features.Main.SystemTray.CleanupTrayIconFeature.Handler>();

            // Main Features - WindowManagement
            services.AddTransient<Features.Main.WindowManagement.OpenSettingsFeature.Handler>();
            services.AddTransient<Features.Main.WindowManagement.ShowLauncherFeature.Handler>();

            // Main Features - Hotkeys
            services.AddTransient<Features.Main.Hotkeys.UpdateWindowHotkeysFeature.Handler>();

            // Main Features - Mode
            services.AddTransient<Features.Main.Mode.EnterFullModeFeature.Handler>();

            // Main Features - AutoSave
            services.AddTransient<Features.Main.AutoSave.ScheduleAutoSaveFeature.Handler>();
        }
    }
}
