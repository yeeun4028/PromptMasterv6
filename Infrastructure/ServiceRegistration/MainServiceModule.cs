using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Main.FileManager;
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
            // ViewModels
            services.AddTransient<FileManagerViewModel>();
            services.AddTransient<ContentEditorViewModel>();
            services.AddTransient<BackupViewModel>();
            services.AddTransient<SidebarViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<TrayViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();
            services.AddSingleton<LaunchBarWindow>();

            // Main Features - Backup
            services.AddSingleton<Features.Main.Backup.PerformCloudBackupFeature.Handler>();
            services.AddSingleton<Features.Main.Backup.PerformLocalBackupFeature.Handler>();

            // Main Features - FileManager
            services.AddSingleton<Features.Main.FileManager.ImportMarkdownFilesFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.ChangeFileIconFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.CreateFolderFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.DeleteFolderFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.RenameFolderFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.ChangeFolderIconFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.CreateFileFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.DeleteFileFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.RenameFileFeature.Handler>();

            // Main Features - Sidebar
            services.AddSingleton<Features.Main.Sidebar.ChangeActionIconFeature.Handler>();

            // Main Features - ContentEditor
            services.AddSingleton<Features.Main.ContentEditor.SearchOnGitHubFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.CopyCompiledTextFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SendToWebTargetFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.OpenWebTargetFeature.Handler>();

            // Main Features - Tray
            services.AddSingleton<Features.Main.Tray.OpenSettingsFeature.Handler>();
            services.AddSingleton<Features.Main.Tray.PinToScreenFromCaptureFeature.Handler>();
            services.AddSingleton<Features.Main.Tray.CleanupTrayIconFeature.Handler>();
        }
    }
}
