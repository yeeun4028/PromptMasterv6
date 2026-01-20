using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Infrastructure.Services;
using PromptMasterv5.ViewModels;
using MessageBox = System.Windows.MessageBox; // 明确指定使用 WPF 的 MessageBox

namespace PromptMasterv5
{
    // 修改这一行，显式继承 System.Windows.Application
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _serviceProvider;
        private System.Threading.Mutex? _singleInstanceMutex;
        private const string MutexName = "PromptMasterv5_SingleInstance_Mutex";
        private const string WindowTitle = "PromptMaster v5";

        public App()
        {
            // Log application start
            LoggerService.Instance.LogInfo("Application starting...", "App");
            
            // 注册全局异常捕获事件
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for existing instance
            bool createdNew;
            _singleInstanceMutex = new System.Threading.Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                LoggerService.Instance.LogInfo("Another instance is already running. Activating existing instance.", "App.OnStartup");
                
                // Try to find and activate the existing window
                ActivateExistingInstance();
                
                // Exit this instance
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

                LoggerService.Instance.LogInfo("Creating main window...", "App.OnStartup");
                MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainWindow.Show();
                
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
            
            // Release the mutex
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }

        private void ActivateExistingInstance()
        {
            try
            {
                // Try to find the existing window by title
                IntPtr hWnd = Infrastructure.Services.NativeMethods.FindWindow(null, WindowTitle);
                
                if (hWnd != IntPtr.Zero)
                {
                    // If the window is minimized, restore it
                    if (Infrastructure.Services.NativeMethods.IsIconic(hWnd))
                    {
                        Infrastructure.Services.NativeMethods.ShowWindow(hWnd, Infrastructure.Services.NativeMethods.SW_RESTORE);
                    }
                    
                    // Bring the window to the foreground
                    Infrastructure.Services.NativeMethods.SetForegroundWindow(hWnd);
                }
            }
            catch (Exception ex)
            {
                // If activation fails, just continue - the new instance will exit anyway
                LoggerService.Instance.LogException(ex, "Failed to activate existing instance", "App.ActivateExistingInstance");
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();

            // Configuration Service (单例，所有 VM 共享配置)
            services.AddSingleton<ISettingsService, SettingsService>();

            // ViewModels
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SidebarViewModel>();
            services.AddSingleton<ChatViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            services.AddSingleton<IAiService, AiService>();
            services.AddSingleton<IDataService>(sp => sp.GetRequiredService<WebDavDataService>());
            services.AddSingleton<WebDavDataService>();
            services.AddSingleton<FileDataService>();
            services.AddSingleton<BrowserAutomationService>();
            services.AddSingleton<GlobalKeyService>();
            services.AddSingleton<FabricService>();
            services.AddHttpClient<BaiduService>();
            services.AddHttpClient<GoogleService>();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LoggerService.Instance.LogException(e.Exception, "Unhandled dispatcher exception", "App.DispatcherUnhandledException");
            MessageBox.Show($"发生未处理异常: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // 防止程序直接崩溃
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
