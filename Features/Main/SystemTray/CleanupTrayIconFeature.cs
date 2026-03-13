using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.SystemTray;

/// <summary>
/// 清理系统托盘图标功能
/// 负责安全释放 NotifyIcon 资源，避免托盘图标残留
/// </summary>
public static class CleanupTrayIconFeature
{
    // 1. 定义输入
    /// <summary>
    /// 清理托盘图标命令
    /// </summary>
    /// <param name="Reason">清理原因（可选，用于日志记录）</param>
    public record Command(string? Reason = null) : IRequest<Result>;

    // 2. 定义输出
    /// <summary>
    /// 清理结果
    /// </summary>
    /// <param name="Success">是否成功</param>
    /// <param name="Message">结果消息</param>
    public record Result(bool Success, string Message);

    // 3. 执行逻辑
    /// <summary>
    /// 清理托盘图标处理器
    /// </summary>
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly TrayService _trayService;
        private readonly LoggerService _logger;

        /// <summary>
        /// 构造函数 - 只注入当前 Feature 绝对需要的服务
        /// </summary>
        public Handler(TrayService trayService, LoggerService logger)
        {
            _trayService = trayService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                var reason = request.Reason ?? "应用程序退出";
                _logger.LogInfo($"[CleanupTrayIconFeature] 开始清理托盘图标，原因: {reason}", "CleanupTrayIconFeature.Handle");

                // 在这里实现从头到尾的业务逻辑
                await Task.Run(() =>
                {
                    _trayService.Dispose();
                }, cancellationToken);

                _logger.LogInfo("[CleanupTrayIconFeature] 托盘图标清理完成", "CleanupTrayIconFeature.Handle");
                return new Result(true, "托盘图标清理成功");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "清理托盘图标时发生异常", "CleanupTrayIconFeature.Handle");
                return new Result(false, $"清理失败: {ex.Message}");
            }
        }
    }
}
