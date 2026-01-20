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

        public App()
        {
            // 注册全局异常捕获事件
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用程序启动严重错误:\n{ex.Message}\n\n{ex.StackTrace}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();

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
            MessageBox.Show($"发生未处理异常: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // 防止程序直接崩溃
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"发生致命错误: {ex.Message}", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"后台任务异常: {e.Exception.Message}");
            e.SetObserved();
        }
    }
}
