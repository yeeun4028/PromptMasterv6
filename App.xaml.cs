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

            // 单实例检查 - 在服务配置之前执行
            var singleInstanceHandler = new Features.AppCore.SingleInstance.EnsureSingleInstanceFeature.Handler(LoggerService.Instance);
            var singleInstanceResult = singleInstanceHandler.Handle(
                new Features.AppCore.SingleInstance.EnsureSingleInstanceFeature.Command(MutexName, WindowTitle),
                System.Threading.CancellationToken.None).Result;

            if (!singleInstanceResult.IsFirstInstance)
            {
                Shutdown();
                return;
            }

            _singleInstanceMutex = singleInstanceResult.Mutex;
            _ownsMutex = singleInstanceResult.OwnsMutex;

            try
            {
                LoggerService.Instance.LogInfo("Configuring services...", "App.OnStartup");
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                // 使用 InitializeApplicationFeature 进行初始化
                var initHandler = _serviceProvider.GetRequiredService<Features.AppCore.Initialization.InitializeApplicationFeature.Handler>();
                var initResult = initHandler.Handle(
                    new Features.AppCore.Initialization.InitializeApplicationFeature.Command(_serviceProvider),
                    System.Threading.CancellationToken.None).Result;

                if (!initResult.Success)
                {
                    throw new Exception(initResult.Message);
                }

                MainWindow = initResult.MainWindow;
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
                // 使用 CleanupApplicationFeature 进行清理
                var cleanupHandler = _serviceProvider?.GetService<Features.AppCore.Shutdown.CleanupApplicationFeature.Handler>();
                if (cleanupHandler != null && _serviceProvider != null)
                {
                    cleanupHandler.Handle(
                        new Features.AppCore.Shutdown.CleanupApplicationFeature.Command(
                            _serviceProvider,
                            _singleInstanceMutex,
                            _ownsMutex),
                        System.Threading.CancellationToken.None).Wait();
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "退出清理失败", "App.OnExit");
            }

            base.OnExit(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // 注册服务模块
            var modules = new List<Infrastructure.ServiceRegistration.IServiceModule>
            {
                new Infrastructure.ServiceRegistration.ApplicationServiceModule(),
                new Infrastructure.ServiceRegistration.MainServiceModule(),
                new Infrastructure.ServiceRegistration.SettingsServiceModule(),
                new Infrastructure.ServiceRegistration.ExternalToolsServiceModule()
            };

            // 遍历所有模块注册服务
            foreach (var module in modules)
            {
                module.RegisterServices(services);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var handler = _serviceProvider?.GetService<Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Handler>();
            handler?.Handle(
                new Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Command(
                    e.Exception, "Dispatcher", ShowMessageToUser: true),
                System.Threading.CancellationToken.None).Wait();
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var handler = _serviceProvider?.GetService<Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Handler>();
            if (e.ExceptionObject is Exception ex)
            {
                handler?.Handle(
                    new Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Command(
                        ex, "AppDomain", e.IsTerminating, ShowMessageToUser: true),
                    System.Threading.CancellationToken.None).Wait();
            }
            else
            {
                LoggerService.Instance.LogError($"Fatal unhandled non-exception object: {e.ExceptionObject}", "App.CurrentDomain_UnhandledException");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            var handler = _serviceProvider?.GetService<Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Handler>();
            handler?.Handle(
                new Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Command(
                    e.Exception, "TaskScheduler", ShowMessageToUser: false),
                System.Threading.CancellationToken.None).Wait();
            e.SetObserved();
        }
    }
}
