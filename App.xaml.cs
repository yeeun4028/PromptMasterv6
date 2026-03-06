using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.ViewModels;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using TextBox = System.Windows.Controls.TextBox;
using WpfControl = System.Windows.Controls.Control;
using Cursors = System.Windows.Input.Cursors;

namespace PromptMasterv6
{
    // 修改这一行，显式继承 System.Windows.Application
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _serviceProvider;

        // Expose ServiceProvider
        public IServiceProvider ServiceProvider => _serviceProvider!;

        private System.Threading.Mutex? _singleInstanceMutex;
        private bool _ownsMutex;
        private const string MutexName = "PromptMasterv6_SingleInstance_Mutex";
        private const string WindowTitle = "PromptMaster v5";

        public App()
        {
            // Log application start
            LoggerService.Instance.LogInfo("Application starting...", "App");
            
            // 注册全局异常捕获事件
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // 【关键修复】：在 App 层面也挂载进程退出事件，确保托盘图标被清理
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                LoggerService.Instance.LogInfo("[App] ProcessExit event triggered", "App");
                CleanupNotifyIcon();
            };
        }

        /// <summary>
        /// 清理托盘图标（静态方法，可从任何地方调用）
        /// </summary>
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

            // ===== 全局 TextBox 右键菜单劫持 =====
            // WPF TextBox 内建菜单是运行时动态生成的，无法通过 XAML ContextMenu 属性覆盖。
            // 必须在类级别注册事件处理器，才能真正替换默认菜单。
            EventManager.RegisterClassHandler(
                typeof(TextBox),
                ContextMenuService.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(OnTextBoxContextMenuOpening));
            // =====================================

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
                
                var launchBarWindow = _serviceProvider.GetRequiredService<PromptMasterv6.Views.LaunchBarWindow>();
                launchBarWindow.Show(); // Trigger Window_Loaded → UpdateVisibility()

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

            // 仅释放资源，不执行保存操作（避免死锁）
            // 保存操作已在 MainWindow.Tray_Exit_Click 中异步完成
            try
            {
                var mainVM = _serviceProvider?.GetService(typeof(MainViewModel)) as MainViewModel;
                if (mainVM != null)
                {
                    LoggerService.Instance.LogInfo("释放 MainViewModel 资源...", "App.OnExit");
                    // 仅释放资源
                    mainVM.Dispose();
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "退出清理失败", "App.OnExit");
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
            services.AddSingleton<PromptMasterv6.Views.LaunchBarWindow>();

            services.AddSingleton<ILauncherService, LauncherService>();
            services.AddSingleton<IAiService, AiService>();
            services.AddSingleton<IDataService>(sp => sp.GetRequiredService<WebDavDataService>());
            services.AddSingleton<WebDavDataService>();
            services.AddSingleton<FileDataService>();
            services.AddSingleton<GlobalKeyService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IWindowManager, WindowManager>();

            // 类型化 HttpClient（BaiduService、GoogleService、TencentService）
            services.AddHttpClient<BaiduService>();
            services.AddHttpClient<GoogleService>();
            services.AddHttpClient<TencentService>();

            services.AddSingleton<ClipboardService>();
            services.AddSingleton<IAudioService, AudioService>();
        }

        /// <summary>
        /// 全局 TextBox 右键菜单替换处理器。
        /// WPF TextBox 的内建菜单是运行时动态生成的，此处理器通过类级别事件劫持，
        /// 在菜单即将弹出前，清空并重新构建一个符合 UIPro 风格的干净菜单。
        /// </summary>
        private static void OnTextBoxContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not TextBox tb) return;

            // 构建样式（在代码里手动实现圆角+阴影+主题色，不依赖 XAML 资源）
            var menu = new ContextMenu
            {
                Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBackground"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["DividerBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 4, 0, 4),
            };

            // 圆角 + 阴影模板
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

            // 菜单项工厂：完整替换 MenuItem 的 ControlTemplate
            // ——只保留文字区，彻底消除图标列和快捷键列。
            static MenuItem MakeItem(string header, ICommand command)
            {
                var item = new MenuItem
                {
                    Header = header,
                    Command = command,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };

                // 自定义 ControlTemplate：仅一个 Border + ContentPresenter
                var itemTemplate = new ControlTemplate(typeof(MenuItem));
                var bd = new FrameworkElementFactory(typeof(Border));
                bd.Name = "Bd";
                bd.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                bd.SetValue(Border.PaddingProperty, new Thickness(12, 3, 12, 3));  // 紧凑间距
                bd.SetValue(Border.CornerRadiusProperty, new System.Windows.CornerRadius(5));

                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetBinding(ContentPresenter.ContentProperty,
                    new System.Windows.Data.Binding(nameof(MenuItem.Header))
                    { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
                bd.AppendChild(cp);
                itemTemplate.VisualTree = bd;

                // 高亮触发器
                var highlight = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                highlight.Setters.Add(new Setter(Border.BackgroundProperty,
                    Application.Current.Resources.Contains("ListItemSelectedBackgroundBrush")
                        ? Application.Current.Resources["ListItemSelectedBackgroundBrush"]
                        : System.Windows.Media.Brushes.LightGray, "Bd"));
                itemTemplate.Triggers.Add(highlight);

                // IsEnabled=false 时半透明
                var disabled = new Trigger { Property = MenuItem.IsEnabledProperty, Value = false };
                disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
                itemTemplate.Triggers.Add(disabled);

                item.Template = itemTemplate;

                // 绑定前景色到主题
                if (Application.Current.Resources["PrimaryTextBrush"] is System.Windows.Media.Brush fg)
                    item.Foreground = fg;

                return item;
            }

            menu.Items.Add(MakeItem("剪切", ApplicationCommands.Cut));
            menu.Items.Add(MakeItem("复制", ApplicationCommands.Copy));
            menu.Items.Add(MakeItem("粘贴", ApplicationCommands.Paste));

            // 分隔线
            var sep = new Separator { Margin = new Thickness(0, 2, 0, 2) };
            if (Application.Current.Resources["DividerBrush"] is System.Windows.Media.Brush divBrush)
                sep.Background = divBrush;
            menu.Items.Add(sep);

            menu.Items.Add(MakeItem("全选", ApplicationCommands.SelectAll));

            // 替换 TextBox 的 ContextMenu
            tb.ContextMenu = menu;
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
