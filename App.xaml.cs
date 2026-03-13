using System;
using System.Windows;
using System.Windows.Threading;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Workspace._LegacyUI;
using PromptMasterv6.Features.Main.ContentEditor;
using PromptMasterv6.Features.Main.Backup;
using PromptMasterv6.Features.Main.Sidebar;
using PromptMasterv6.Features.Main.Tray;
using PromptMasterv6.Features.Main.SystemTray;
using PromptMasterv6.Features.ExternalTools;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Workspace;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.AiModels._LegacyUI;
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

        public static IServiceProvider Services => ((App)Current)._serviceProvider!;

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

                var mediator = _serviceProvider.GetRequiredService<IMediator>();
                await mediator.Send(
                    new CleanupTrayIconFeature.Command("ProcessExit"),
                    System.Threading.CancellationToken.None);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to cleanup notify icon", "App.CleanupNotifyIconAsync");
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                LoggerService.Instance.LogInfo("Configuring services...", "App.OnStartup");
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                var mediator = _serviceProvider.GetRequiredService<IMediator>();
                var singleInstanceResult = await mediator.Send(
                    new Features.AppCore.SingleInstance.EnsureSingleInstanceFeature.Command(MutexName, WindowTitle)
                );

                if (!singleInstanceResult.IsFirstInstance)
                {
                    LoggerService.Instance.LogInfo("Another instance is already running, exiting", "App.OnStartup");
                    Shutdown();
                    return;
                }

                _singleInstanceMutex = singleInstanceResult.Mutex;
                _ownsMutex = singleInstanceResult.OwnsMutex;

                var initResult = await mediator.Send(
                    new Features.AppCore.Initialization.InitializeApplicationFeature.Command()
                );

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

        protected override async void OnExit(ExitEventArgs e)
        {
            LoggerService.Instance.LogInfo($"Application exiting with code: {e.ApplicationExitCode}", "App.OnExit");

            try
            {
                if (_serviceProvider != null)
                {
                    var mediator = _serviceProvider.GetRequiredService<IMediator>();
                    await mediator.Send(
                        new Features.AppCore.Shutdown.CleanupApplicationFeature.Command(
                            _serviceProvider,
                            _singleInstanceMutex,
                            _ownsMutex)
                    );
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
            var modules = new List<Infrastructure.ServiceRegistration.IServiceModule>
            {
                new Infrastructure.ServiceRegistration.ApplicationServiceModule(),
                new Infrastructure.ServiceRegistration.MainServiceModule(),
                new Infrastructure.ServiceRegistration.SettingsServiceModule(),
                new Infrastructure.ServiceRegistration.ExternalToolsServiceModule()
            };

            foreach (var module in modules)
            {
                module.RegisterServices(services);
            }
        }

        private async void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                if (_serviceProvider != null)
                {
                    var mediator = _serviceProvider.GetRequiredService<IMediator>();
                    await mediator.Send(
                        new Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Command(
                            e.Exception, "Dispatcher", ShowMessageToUser: true)
                    );
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to handle dispatcher exception", "App.App_DispatcherUnhandledException");
            }
            finally
            {
                e.Handled = true;
            }
        }

        private async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (_serviceProvider != null && e.ExceptionObject is Exception ex)
                {
                    var mediator = _serviceProvider.GetRequiredService<IMediator>();
                    await mediator.Send(
                        new Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Command(
                            ex, "AppDomain", e.IsTerminating, ShowMessageToUser: true)
                    );
                }
                else
                {
                    LoggerService.Instance.LogError($"Fatal unhandled non-exception object: {e.ExceptionObject}", "App.CurrentDomain_UnhandledException");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to handle appdomain exception", "App.CurrentDomain_UnhandledException");
            }
        }

        private async void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                if (_serviceProvider != null)
                {
                    var mediator = _serviceProvider.GetRequiredService<IMediator>();
                    await mediator.Send(
                        new Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Command(
                            e.Exception, "TaskScheduler", ShowMessageToUser: false)
                    );
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to handle task scheduler exception", "App.TaskScheduler_UnobservedTaskException");
            }
            finally
            {
                e.SetObserved();
            }
        }
    }
}
