using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;

namespace PromptMasterv6.Features.AppCore.Shutdown
{
    /// <summary>
    /// 应用程序退出清理功能切片
    /// 封装应用程序退出时的清理流程
    /// </summary>
    public static class CleanupApplicationFeature
    {
        // 1. 定义输入
        public record Command(
            IServiceProvider ServiceProvider,
            System.Threading.Mutex? Mutex,
            bool OwnsMutex
        );

        // 2. 定义输出
        public record Result(
            bool Success,
            string Message
        );

        // 3. 执行逻辑
        public class Handler
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
                    _logger.LogInfo("Starting application cleanup...", "CleanupApplicationFeature");

                    var serviceProvider = request.ServiceProvider;

                    // 1. 释放 MainViewModel 资源
                    _logger.LogInfo("Disposing MainViewModel resources...", "CleanupApplicationFeature");
                    var mainVM = serviceProvider?.GetService(typeof(MainViewModel)) as MainViewModel;
                    if (mainVM != null)
                    {
                        mainVM.Dispose();
                        _logger.LogInfo("MainViewModel disposed successfully.", "CleanupApplicationFeature");
                    }

                    // 2. 清理托盘图标
                    _logger.LogInfo("Cleaning up tray icon...", "CleanupApplicationFeature");
                    var trayCleanupHandler = serviceProvider?.GetService(
                        typeof(Features.Main.Tray.CleanupTrayIconFeature.Handler))
                        as Features.Main.Tray.CleanupTrayIconFeature.Handler;
                    
                    if (trayCleanupHandler != null)
                    {
                        await trayCleanupHandler.Handle(
                            new Features.Main.Tray.CleanupTrayIconFeature.Command("ApplicationExit"),
                            cancellationToken);
                    }

                    // 3. 释放单实例锁
                    _logger.LogInfo("Releasing single instance mutex...", "CleanupApplicationFeature");
                    var releaseHandler = new Features.AppCore.SingleInstance.ReleaseSingleInstanceFeature.Handler(_logger);
                    await releaseHandler.Handle(
                        new Features.AppCore.SingleInstance.ReleaseSingleInstanceFeature.Command(
                            request.Mutex,
                            request.OwnsMutex),
                        cancellationToken);

                    // 4. 释放服务提供者
                    _logger.LogInfo("Disposing service provider...", "CleanupApplicationFeature");
                    (serviceProvider as ServiceProvider)?.Dispose();

                    _logger.LogInfo("Application cleanup completed successfully.", "CleanupApplicationFeature");

                    return new Result(
                        Success: true,
                        Message: "应用程序清理成功"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to cleanup application", "CleanupApplicationFeature");
                    return new Result(
                        Success: false,
                        Message: $"应用程序清理失败: {ex.Message}"
                    );
                }
            }
        }
    }
}
