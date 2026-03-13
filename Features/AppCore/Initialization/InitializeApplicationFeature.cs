using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Main.Backup;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.ExternalTools;
using PromptMasterv6.Infrastructure.WindowRegistration;

namespace PromptMasterv6.Features.AppCore.Initialization
{
    /// <summary>
    /// 应用程序初始化功能切片
    /// 封装应用程序启动时的初始化流程
    /// </summary>
    public static class InitializeApplicationFeature
    {
        // 1. 定义输入
        public record Command(
            IServiceProvider ServiceProvider
        ) : IRequest<Result>;

        // 2. 定义输出
        public record Result(
            bool Success,
            string Message,
            MainWindow? MainWindow,
            LaunchBarWindow? LaunchBarWindow
        );

        // 3. 执行逻辑
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly LoggerService _logger;

            // 只注入当前 Feature 绝对需要的服务
            public Handler(LoggerService logger)
            {
                _logger = logger;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    _logger.LogInfo("Starting application initialization...", "InitializeApplicationFeature");

                    var serviceProvider = request.ServiceProvider;

                    // 1. 配置 TextBox 上下文菜单
                    _logger.LogInfo("Configuring TextBox context menu...", "InitializeApplicationFeature");
                    var textBoxMenuHandler = serviceProvider.GetService(
                        typeof(Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler))
                        as Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler;
                    
                    if (textBoxMenuHandler != null)
                    {
                        await textBoxMenuHandler.Handle(
                            new Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Command(),
                            cancellationToken);
                    }

                    // 2. 注册窗口
                    _logger.LogInfo("Registering windows...", "InitializeApplicationFeature");
                    var windowRegistry = serviceProvider.GetRequiredService<WindowRegistry>();
                    
                    // 获取所有窗口注册器并注册窗口
                    var registrars = serviceProvider.GetServices<IWindowRegistrar>();
                    foreach (var registrar in registrars)
                    {
                        registrar.Register(windowRegistry);
                    }

                    // 3. 创建主窗口
                    _logger.LogInfo("Creating main window...", "InitializeApplicationFeature");
                    var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
                    mainWindow.Show();

                    // 4. 创建启动栏窗口
                    _logger.LogInfo("Creating launch bar window...", "InitializeApplicationFeature");
                    var launchBarWindow = serviceProvider.GetRequiredService<LaunchBarWindow>();
                    launchBarWindow.Show();

                    // 5. 启动全局快捷键协调器
                    _logger.LogInfo("Starting global shortcut coordinator...", "InitializeApplicationFeature");
                    var shortcutCoordinator = serviceProvider.GetRequiredService<GlobalShortcutCoordinator>();
                    shortcutCoordinator.Start();

                    // 6. 初始化外部工具视图模型
                    _logger.LogInfo("Initializing external tools view model...", "InitializeApplicationFeature");
                    _ = serviceProvider.GetRequiredService<ExternalToolsViewModel>();

                    // 7. 初始化备份视图模型（确保消息监听器注册）
                    _logger.LogInfo("Initializing backup view model...", "InitializeApplicationFeature");
                    _ = serviceProvider.GetRequiredService<BackupViewModel>();

                    _logger.LogInfo("Application initialized successfully.", "InitializeApplicationFeature");

                    var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
                    mainViewModel.Initialize();

                    return new Result(
                        Success: true,
                        Message: "应用程序初始化成功",
                        MainWindow: mainWindow,
                        LaunchBarWindow: launchBarWindow
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to initialize application", "InitializeApplicationFeature");
                    return new Result(
                        Success: false,
                        Message: $"应用程序初始化失败: {ex.Message}",
                        MainWindow: null,
                        LaunchBarWindow: null
                    );
                }
            }
        }
    }
}
