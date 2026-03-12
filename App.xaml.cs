using System;
using System.Windows;
using System.Windows.Threading;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Main.FileManager;
using PromptMasterv6.Features.Main.ContentEditor;
using PromptMasterv6.Features.Main.Backup;
using PromptMasterv6.Features.Main.Sidebar;
using PromptMasterv6.Features.Main.Tray;
using PromptMasterv6.Features.ExternalTools;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Workspace;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.ExternalTools;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Features.Shared.Dialogs;
using PromptMasterv6.Features.Shared.Behaviors;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PromptMasterv6
{
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _serviceProvider;

        public IServiceProvider ServiceProvider => _serviceProvider!;

        private System.Threading.Mutex? _singleInstanceMutex;
        private bool _ownsMutex;
        private const string MutexName = "PromptMasterv6_SingleInstance_Mutex";
        private const string WindowTitle = "PromptMaster v6";

        public App()
        {
            LoggerService.Instance.LogInfo("Application starting...", "App");
            
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
            {
                LoggerService.Instance.LogInfo("[App] ProcessExit event triggered", "App");
                await CleanupNotifyIconAsync();
            };
        }

        private async Task CleanupNotifyIconAsync()
        {
            try
            {
                if (_serviceProvider == null) return;

                var handler = _serviceProvider.GetService<Features.Main.Tray.CleanupTrayIconFeature.Handler>();
                if (handler != null)
                {
                    await handler.Handle(new Features.Main.Tray.CleanupTrayIconFeature.Command("ProcessExit"), System.Threading.CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to cleanup notify icon", "App.CleanupNotifyIconAsync");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            _singleInstanceMutex = new System.Threading.Mutex(true, MutexName, out createdNew);
            _ownsMutex = createdNew;

            if (!createdNew)
            {
                LoggerService.Instance.LogInfo("Another instance is already running. Activating existing instance.", "App.OnStartup");
                ActivateExistingInstance();
                Shutdown();
                return;
            }

            LoggerService.Instance.LogInfo("Single instance mutex acquired successfully.", "App.OnStartup");

            try
            {
                LoggerService.Instance.LogInfo("Configuring services...", "App.OnStartup");
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                // 配置 TextBox 上下文菜单
                var textBoxMenuHandler = _serviceProvider.GetService<Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler>();
                textBoxMenuHandler?.Handle(new Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Command(), System.Threading.CancellationToken.None);

                var windowRegistry = _serviceProvider.GetRequiredService<WindowRegistry>();
                RegisterWindows(windowRegistry);

                LoggerService.Instance.LogInfo("Creating main window...", "App.OnStartup");
                MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainWindow.Show();
                
                var launchBarWindow = _serviceProvider.GetRequiredService<LaunchBarWindow>();
                launchBarWindow.Show();

                var shortcutCoordinator = _serviceProvider.GetRequiredService<GlobalShortcutCoordinator>();
                shortcutCoordinator.Start();

                _ = _serviceProvider.GetRequiredService<ExternalToolsViewModel>();

                LoggerService.Instance.LogInfo("Application started successfully.", "App.OnStartup");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Fatal error during application startup", "App.OnStartup");
                MessageBox.Show($"应用程序启动严重错误:\n{ex.Message}\n\n{ex.StackTrace}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggerService.Instance.LogInfo($"Application exiting with code: {e.ApplicationExitCode}", "App.OnExit");

            try
            {
                var mainVM = _serviceProvider?.GetService(typeof(MainViewModel)) as MainViewModel;
                if (mainVM != null)
                {
                    LoggerService.Instance.LogInfo("释放 MainViewModel 资源...", "App.OnExit");
                    mainVM.Dispose();
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "退出清理失败", "App.OnExit");
            }

            if (_ownsMutex && _singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            _singleInstanceMutex?.Dispose();

            _serviceProvider?.Dispose();
            base.OnExit(e);
        }

        private void ActivateExistingInstance()
        {
            try
            {
                IntPtr hWnd = Infrastructure.Services.NativeMethods.FindWindow(null, WindowTitle);
                
                if (hWnd != IntPtr.Zero)
                {
                    if (Infrastructure.Services.NativeMethods.IsIconic(hWnd))
                    {
                        Infrastructure.Services.NativeMethods.ShowWindow(hWnd, Infrastructure.Services.NativeMethods.SW_RESTORE);
                    }
                    
                    Infrastructure.Services.NativeMethods.SetForegroundWindow(hWnd);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to activate existing instance", "App.ActivateExistingInstance");
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<LoggerService>(sp => LoggerService.Instance);

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(App).Assembly);
                cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
            });

            // Application Features
            services.AddSingleton<Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler>();

            services.AddSingleton<ISessionState, SessionState>();

            services.AddTransient<ZhipuCompatHandler>();

            services.AddHttpClient("AiServiceClient")
                .AddHttpMessageHandler<ZhipuCompatHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            
            services.AddHttpClient("NativeAiClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddSingleton<SettingsService>();
            services.AddSingleton<AppConfig>(sp => sp.GetRequiredService<SettingsService>().Config);
            services.AddSingleton<WindowRegistry>();

            services.AddTransient<SettingsViewModel>();
            services.AddSingleton<ExternalToolsViewModel>();
            services.AddTransient<LauncherViewModel>();
            services.AddTransient<WorkspaceViewModel>();
            services.AddTransient<FileManagerViewModel>();
            services.AddTransient<ContentEditorViewModel>();
            services.AddTransient<BackupViewModel>();
            services.AddTransient<SidebarViewModel>();
            services.AddSingleton<MainViewModel>();

            services.AddSingleton<MainWindow>();
            services.AddSingleton<LaunchBarWindow>();
            services.AddTransient<LauncherWindow>();
            services.AddTransient<SettingsWindow>();

            services.AddSingleton<AiService>();
            services.AddSingleton<FileDataService>();
            services.AddSingleton<WebDavDataService>();
            services.AddKeyedSingleton<IDataService>("cloud", (sp, key) => sp.GetRequiredService<WebDavDataService>());
            services.AddKeyedSingleton<IDataService>("local", (sp, key) => sp.GetRequiredService<FileDataService>());
            services.AddSingleton<IDataService>(sp => sp.GetRequiredService<WebDavDataService>());
            services.AddSingleton<GlobalKeyService>();
            services.AddSingleton<HotkeyService>();
            services.AddSingleton<GlobalShortcutCoordinator>();
            services.AddSingleton<DialogService>();
            services.AddSingleton<WindowManager>();

            services.AddHttpClient<BaiduService>();
            services.AddHttpClient<GoogleService>();
            services.AddHttpClient<TencentService>();

            services.AddSingleton<ClipboardService>();
            services.AddSingleton<TrayViewModel>();
            services.AddSingleton<TrayService>();

            services.AddSingleton<Features.Settings.AiModels.TestAiConnectionFeature.Handler>();
            services.AddSingleton<Features.Settings.AiModels.DeleteAiModelFeature.Handler>();
            services.AddSingleton<Features.Main.Backup.PerformCloudBackupFeature.Handler>();

            services.AddSingleton<Features.Main.FileManager.ImportMarkdownFilesFeature.Handler>();
            services.AddSingleton<Features.Main.Backup.PerformLocalBackupFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.ChangeFileIconFeature.Handler>();

            services.AddSingleton<Features.Main.Sidebar.ChangeActionIconFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SearchOnGitHubFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.CopyCompiledTextFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.SendToWebTargetFeature.Handler>();
            services.AddSingleton<Features.Main.ContentEditor.OpenWebTargetFeature.Handler>();

            services.AddSingleton<Features.Main.Tray.OpenSettingsFeature.Handler>();
            services.AddSingleton<Features.Main.Tray.PinToScreenFromCaptureFeature.Handler>();
            services.AddSingleton<Features.Main.Tray.CleanupTrayIconFeature.Handler>();

            services.AddSingleton<Features.Main.FileManager.CreateFolderFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.DeleteFolderFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.RenameFolderFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.ChangeFolderIconFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.CreateFileFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.DeleteFileFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.RenameFileFeature.Handler>();
            services.AddSingleton<Features.Main.FileManager.ChangeFileIconFeature.Handler>();

            services.AddSingleton<Features.Settings.Launcher.AddSearchPathFeature.Handler>();
            services.AddSingleton<Features.Settings.Launcher.RemoveSearchPathFeature.Handler>();

            services.AddSingleton<Features.Settings.Sync.ManualRestoreFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ManualLocalRestoreFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ManualBackupFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ExportConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.Sync.ImportConfigFeature.Handler>();

            services.AddSingleton<Features.Settings.ExternalTools.SaveAiTranslationConfigFeature.Handler>();
            services.AddSingleton<Features.Settings.ExternalTools.DeleteAiTranslationConfigFeature.Handler>();

            services.AddSingleton<Features.Settings.LaunchBar.AddLaunchBarItemFeature.Handler>();
            services.AddSingleton<Features.Settings.LaunchBar.RemoveLaunchBarItemFeature.Handler>();
            services.AddSingleton<Features.Settings.LaunchBar.MoveLaunchBarItemFeature.Handler>();

            services.AddSingleton<Features.Settings.ApiCredentials.TestBaiduOcrFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestBaiduTranslateFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestTencentOcrFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestTencentTranslateFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.TestGoogleFeature.Handler>();
            services.AddSingleton<Features.Settings.ApiCredentials.SaveApiCredentialsFeature.Handler>();

            // Workspace Features
            services.AddSingleton<Features.Workspace.LoadWorkspaceData.LoadWorkspaceDataFeature.Handler>();
            services.AddSingleton<Features.Workspace.SearchOnGitHub.SearchOnGitHubFeature.Handler>();
            services.AddSingleton<Features.Workspace.ChangeFileIcon.ChangeFileIconFeature.Handler>();
            services.AddSingleton<Features.Workspace.DeleteFile.DeleteFileFeature.Handler>();

            // Launcher Features
            services.AddSingleton<Features.Launcher.ReorderLauncherItems.ReorderLauncherItemsFeature.Handler>();
            services.AddSingleton<Features.Launcher.FilterLauncherItems.FilterLauncherItemsFeature.Handler>();

            // ExternalTools Features
            services.AddSingleton<Features.ExternalTools.PerformOcr.PerformOcrFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformScreenshotOcr.PerformScreenshotOcrFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformTranslate.PerformTranslateFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformVisionTranslate.PerformVisionTranslateFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformScreenshotTranslate.PerformScreenshotTranslateFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.EnsureAiProfile.EnsureAiProfileFeature.Handler>();

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

        }

        private static void RegisterWindows(WindowRegistry registry)
        {
            registry.RegisterWindow<LauncherViewModel, LauncherWindow>();
            registry.RegisterWindow<SettingsViewModel, SettingsWindow>();

            registry.RegisterScreenCaptureOverlay((screenBitmap, onCaptureProcessing) =>
                new Features.ExternalTools.ScreenCaptureOverlay(screenBitmap, onCaptureProcessing));

            registry.RegisterTranslationPopup(text =>
                new Features.ExternalTools.TranslationPopup(text));

            registry.RegisterPinToScreen(Features.PinToScreen.PinToScreenWindow.PinToScreenAsync);
        }


        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LoggerService.Instance.LogException(e.Exception, "Unhandled dispatcher exception", "App.DispatcherUnhandledException");
            MessageBox.Show($"发生未处理异常: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Fatal unhandled exception. IsTerminating: {e.IsTerminating}", "App.CurrentDomain_UnhandledException");
                MessageBox.Show($"发生致命错误: {ex.Message}", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                LoggerService.Instance.LogError($"Fatal unhandled non-exception object: {e.ExceptionObject}", "App.CurrentDomain_UnhandledException");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            LoggerService.Instance.LogException(e.Exception, "Unobserved task exception", "App.TaskScheduler_UnobservedTaskException");
            System.Diagnostics.Debug.WriteLine($"后台任务异常: {e.Exception.Message}");
            e.SetObserved();
        }
    }
}
