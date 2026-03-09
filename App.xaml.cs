using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Sidebar;
using PromptMasterv6.Features.ExternalTools;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Workspace;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.Sync;
using PromptMasterv6.Features.Settings.Launcher;
using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Features.Shared.Dialogs;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using TextBox = System.Windows.Controls.TextBox;
using WpfControl = System.Windows.Controls.Control;
using Cursors = System.Windows.Input.Cursors;

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

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                LoggerService.Instance.LogInfo("[App] ProcessExit event triggered", "App");
                CleanupNotifyIcon();
            };
        }

        private static void CleanupNotifyIcon()
        {
            try
            {
                var mainWindow = Current?.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ForceCleanupNotifyIcon();
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to cleanup notify icon", "App.CleanupNotifyIcon");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            EventManager.RegisterClassHandler(
                typeof(TextBox),
                ContextMenuService.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(OnTextBoxContextMenuOpening));

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

                LoggerService.Instance.LogInfo("Creating main window...", "App.OnStartup");
                MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainWindow.Show();
                
                var launchBarWindow = _serviceProvider.GetRequiredService<LaunchBarWindow>();
                launchBarWindow.Show();

                var shortcutCoordinator = _serviceProvider.GetRequiredService<IGlobalShortcutCoordinator>();
                shortcutCoordinator.Start();

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
            services.AddTransient<ZhipuCompatHandler>();

            services.AddHttpClient("AiServiceClient")
                .AddHttpMessageHandler<ZhipuCompatHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            
            services.AddHttpClient("NativeAiClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddSingleton<ISettingsService, SettingsService>();

            services.AddSingleton<IWindowRegistry, WindowRegistry>();

            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SidebarViewModel>();
            services.AddTransient<ExternalToolsViewModel>();
            services.AddTransient<LauncherViewModel>();
            services.AddTransient<WorkspaceViewModel>();
            services.AddTransient<MainViewModel>();

            services.AddSingleton<MainWindow>();
            services.AddSingleton<LaunchBarWindow>();
            services.AddTransient<LauncherWindow>();
            services.AddTransient<SettingsWindow>();

            services.AddSingleton<ILauncherService, LauncherService>();
            services.AddSingleton<IAiService, AiService>();
            services.AddKeyedSingleton<IDataService, WebDavDataService>("cloud");
            services.AddKeyedSingleton<IDataService, FileDataService>("local");
            services.AddSingleton<IGlobalKeyService, GlobalKeyService>();
            services.AddSingleton<IHotkeyService, HotkeyService>();
            services.AddSingleton<IGlobalShortcutCoordinator, GlobalShortcutCoordinator>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IWindowManager, WindowManager>();

            services.AddHttpClient<IBaiduService, BaiduService>();
            services.AddHttpClient<IGoogleService, GoogleService>();
            services.AddHttpClient<ITencentService, TencentService>();

            services.AddSingleton<IClipboardService, ClipboardService>();

            services.AddSingleton<IVariableService, VariableService>();
            services.AddSingleton<IContentConverterService, ContentConverterService>();
            services.AddSingleton<IWebTargetService, WebTargetService>();

            services.AddSingleton<AiModelsViewModel>();
            services.AddSingleton<SyncViewModel>();
            services.AddSingleton<LauncherSettingsViewModel>();
            services.AddSingleton<SettingsContainerViewModel>();
            services.AddSingleton<ApiCredentialsViewModel>();

        }

        private static void RegisterWindows(IWindowRegistry registry)
        {
            registry.RegisterWindow<LauncherViewModel, LauncherWindow>();
            registry.RegisterWindow<SettingsViewModel, SettingsWindow>();

            registry.RegisterScreenCaptureOverlay((screenBitmap, onCaptureProcessing) =>
                new Features.ExternalTools.ScreenCaptureOverlay(screenBitmap, onCaptureProcessing));

            registry.RegisterTranslationPopup(text =>
                new Features.ExternalTools.TranslationPopup(text));

            registry.RegisterPinToScreen(Features.PinToScreen.PinToScreenWindow.PinToScreenAsync);
            registry.RegisterPinToScreenCloseAll(Features.PinToScreen.PinToScreenWindow.CloseAll);
            registry.RegisterPinToScreenCountProvider(() => Features.PinToScreen.PinToScreenWindow.OpenWindowCount);
        }

        private static void OnTextBoxContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not TextBox tb) return;

            var menu = new ContextMenu
            {
                Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBackground"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["DividerBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 4, 0, 4),
            };

            var menuTemplate = new ControlTemplate(typeof(ContextMenu));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath(WpfControl.BackgroundProperty) });
            borderFactory.SetBinding(Border.BorderBrushProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath(WpfControl.BorderBrushProperty) });
            borderFactory.SetBinding(Border.BorderThicknessProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath(WpfControl.BorderThicknessProperty) });
            borderFactory.SetValue(Border.CornerRadiusProperty, new System.Windows.CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
            var shadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                Direction = 270,
                ShadowDepth = 4,
                BlurRadius = 12,
                Opacity = 0.3
            };
            borderFactory.SetValue(Border.EffectProperty, shadow);
            var itemsPresenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter));
            borderFactory.AppendChild(itemsPresenterFactory);
            menuTemplate.VisualTree = borderFactory;
            menu.Template = menuTemplate;

            static MenuItem MakeItem(string header, ICommand command)
            {
                var item = new MenuItem
                {
                    Header = header,
                    Command = command,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };

                var itemTemplate = new ControlTemplate(typeof(MenuItem));
                var bd = new FrameworkElementFactory(typeof(Border));
                bd.Name = "Bd";
                bd.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                bd.SetValue(Border.PaddingProperty, new Thickness(12, 3, 12, 3));
                bd.SetValue(Border.CornerRadiusProperty, new System.Windows.CornerRadius(5));

                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetBinding(ContentPresenter.ContentProperty,
                    new System.Windows.Data.Binding(nameof(MenuItem.Header))
                    { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
                bd.AppendChild(cp);
                itemTemplate.VisualTree = bd;

                var highlight = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                highlight.Setters.Add(new Setter(Border.BackgroundProperty,
                    Application.Current.Resources.Contains("ListItemSelectedBackgroundBrush")
                        ? Application.Current.Resources["ListItemSelectedBackgroundBrush"]
                        : System.Windows.Media.Brushes.LightGray, "Bd"));
                itemTemplate.Triggers.Add(highlight);

                var disabled = new Trigger { Property = MenuItem.IsEnabledProperty, Value = false };
                disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
                itemTemplate.Triggers.Add(disabled);

                item.Template = itemTemplate;

                if (Application.Current.Resources["PrimaryTextBrush"] is System.Windows.Media.Brush fg)
                    item.Foreground = fg;

                return item;
            }

            menu.Items.Add(MakeItem("剪切", ApplicationCommands.Cut));
            menu.Items.Add(MakeItem("复制", ApplicationCommands.Copy));
            menu.Items.Add(MakeItem("粘贴", ApplicationCommands.Paste));

            var sep = new Separator { Margin = new Thickness(0, 2, 0, 2) };
            if (Application.Current.Resources["DividerBrush"] is System.Windows.Media.Brush divBrush)
                sep.Background = divBrush;
            menu.Items.Add(sep);

            menu.Items.Add(MakeItem("全选", ApplicationCommands.SelectAll));

            tb.ContextMenu = menu;
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
