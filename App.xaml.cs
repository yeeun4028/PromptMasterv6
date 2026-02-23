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

        // Expose ServiceProvider
        public IServiceProvider ServiceProvider => _serviceProvider!;

        private System.Threading.Mutex? _singleInstanceMutex;
        private bool _ownsMutex;
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
            _ownsMutex = createdNew;

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
                
                // Initialize Voice Hotkey
                var keyService = _serviceProvider.GetRequiredService<GlobalKeyService>();
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                keyService.UpdateVoiceHotkey(settingsService.Config.VoiceTriggerHotkey);

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
            
            // 在退出前强制执行一次本地保存（仅对托盘菜单退出有效，taskkill /F 无效）
            try
            {
                var mainVM = _serviceProvider?.GetService(typeof(MainViewModel)) as MainViewModel;
                if (mainVM != null)
                {
                    LoggerService.Instance.LogInfo("执行退出前保存...", "App.OnExit");
                    // 使用 GetAwaiter().GetResult() 避免 Wait() 的死锁风险
                    // 因为 OnExit 已经在 UI 线程，不需要同步上下文
                    mainVM.PerformLocalBackup().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "退出前保存失败", "App.OnExit");
            }
            
            // Release the mutex
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
            // 注册 ZhipuCompatHandler（智谱 AI URL 兼容性处理器）
            services.AddTransient<ZhipuCompatHandler>();

            // 注册具名 HttpClient（用于 AiService）
            services.AddHttpClient("AiServiceClient")
                .AddHttpMessageHandler<ZhipuCompatHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            
            services.AddHttpClient("NativeAiClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            // 注册 VoiceClient（用于语音识别）
            services.AddHttpClient("VoiceClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            // Configuration Service (单例，所有 VM 共享配置)
            services.AddSingleton<ISettingsService, SettingsService>();

            // ViewModels
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SidebarViewModel>();
            services.AddSingleton<ChatViewModel>();
            services.AddSingleton<ExternalToolsViewModel>();
            services.AddTransient<LauncherViewModel>(); 
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            services.AddSingleton<ILauncherService, LauncherService>();
            services.AddSingleton<IAiService, AiService>();
            services.AddSingleton<IDataService>(sp => sp.GetRequiredService<WebDavDataService>());
            services.AddSingleton<WebDavDataService>();
            services.AddSingleton<FileDataService>();
            services.AddSingleton<GlobalKeyService>();
            services.AddSingleton<FabricService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IWindowManager, WindowManager>();

            // 类型化 HttpClient（BaiduService、GoogleService、TencentService）
            services.AddHttpClient<BaiduService>();
            services.AddHttpClient<GoogleService>();
            services.AddHttpClient<TencentService>();

            // Voice Control Services
            services.AddSingleton<IVoiceService, VoiceService>();
            services.AddSingleton<ICommandExecutionService, CommandExecutionService>();
            services.AddTransient<VoiceControlViewModel>();
            
            // 全局划词助手服务
            services.AddSingleton<WindowPositionService>();
            services.AddSingleton<ClipboardService>();
            services.AddSingleton<IAudioService, AudioService>();
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
