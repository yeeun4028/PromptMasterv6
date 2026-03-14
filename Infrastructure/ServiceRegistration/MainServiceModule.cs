using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Workspace._LegacyUI;
using PromptMasterv6.Features.Main.ContentEditor;
using PromptMasterv6.Features.Main.Backup;
using PromptMasterv6.Features.Main.Sidebar;
using PromptMasterv6.Features.Main.Tray;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.FolderTree;
using PromptMasterv6.Features.Workspace.FileList;
using PromptMasterv6.Features.Workspace;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    public class MainServiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IWorkspaceState, WorkspaceState>();
            
            services.AddSingleton<FolderTreeViewModel>();
            services.AddSingleton<FileListViewModel>();
            services.AddSingleton<WorkspaceContainerViewModel>();
            
            services.AddSingleton<FileManagerViewModel>();
            services.AddSingleton<ContentEditorViewModel>();
            services.AddSingleton<BackupViewModel>();
            services.AddSingleton<SidebarViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<TrayViewModel>();

            services.AddSingleton<MainWindow>();
            services.AddSingleton<LaunchBarWindow>();

            services.AddSingleton<Features.Main.Backup.PerformCloudBackupFeature.Handler>();
            services.AddSingleton<Features.Main.Backup.PerformLocalBackupFeature.Handler>();

            services.AddSingleton<Features.Workspace.ImportFiles.ImportMarkdownFilesFeature.Handler>();
            services.AddSingleton<Features.Workspace.SelectFilesForImport.SelectFilesForImportFeature.Handler>();
            services.AddSingleton<Features.Workspace.MoveFile.MoveFileToFolderFeature.Handler>();
            services.AddSingleton<Features.Workspace.InitializeAppData.InitializeAppDataFeature.Handler>();
            services.AddSingleton<Features.Workspace.ChangeFileIcon.ChangeFileIconFeature.Handler>();
            services.AddSingleton<Features.Workspace.CreateFolder.CreateFolderFeature.Handler>();
            services.AddSingleton<Features.Workspace.DeleteFolder.DeleteFolderFeature.Handler>();
            services.AddSingleton<Features.Workspace.RenameFolder.RenameFolderFeature.Handler>();
            services.AddSingleton<Features.Workspace.ChangeFolderIcon.ChangeFolderIconFeature.Handler>();
            services.AddSingleton<Features.Workspace.CreateFile.CreateFileFeature.Handler>();
            services.AddSingleton<Features.Workspace.DeleteFile.DeleteFileFeature.Handler>();
            services.AddSingleton<Features.Workspace.RenameFile.RenameFileFeature.Handler>();
            services.AddSingleton<Features.Workspace.CompleteFileRename.CompleteFileRenameFeature.Handler>();
            services.AddSingleton<Features.Workspace.CancelFileRename.CancelFileRenameFeature.Handler>();
            services.AddSingleton<Features.Workspace.LoadAppData.LoadAppDataFeature.Handler>();
            services.AddSingleton<Features.Workspace.SaveAppData.SaveAppDataFeature.Handler>();
            services.AddSingleton<Features.Workspace.FileList.GetFilesByFolderFeature.Handler>();
            services.AddSingleton<Features.Workspace.ReorderFolder.ReorderFolderFeature.Handler>();

            services.AddSingleton<Features.Main.Sidebar.ChangeActionIconFeature.Handler>();

            services.AddSingleton<Features.Main.ContentEditor.SetCurrentFileFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SyncVariablesFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.ToggleEditModeFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SearchOnGitHubFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.CopyCompiledTextFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SendToWebTargetFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.OpenWebTargetFeature.Handler>();

            services.AddSingleton<Features.Main.Tray.OpenSettingsFeature.Handler>();
            services.AddSingleton<Features.Main.Tray.PinToScreenFromCaptureFeature.Handler>();
            services.AddSingleton<Features.Main.SystemTray.CleanupTrayIconFeature.Handler>();

            services.AddTransient<Features.Main.WindowManagement.OpenSettingsFeature.Handler>();
            services.AddTransient<Features.Main.WindowManagement.ShowLauncherFeature.Handler>();

            services.AddTransient<Features.Main.Hotkeys.UpdateWindowHotkeysFeature.Handler>();

            services.AddTransient<Features.Main.Mode.EnterFullModeFeature.Handler>();

            services.AddTransient<Features.Main.AutoSave.ScheduleAutoSaveFeature.Handler>();
        }
    }
}
