using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Sync.ClearLogs;

/// <summary>
/// 清空日志功能
/// </summary>
public static class ClearLogsFeature
{
    /// <summary>
    /// 定义输入（必须实现 IRequest）
    /// </summary>
    public record Command() : IRequest<Result>;

    /// <summary>
    /// 定义输出
    /// </summary>
    public record Result(bool Success, string Message);

    /// <summary>
    /// 执行逻辑（必须实现 IRequestHandler）
    /// </summary>
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly LoggerService _logger;

        /// <summary>
        /// 只注入当前 Feature 绝对需要的服务
        /// </summary>
        public Handler(LoggerService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 必须带有 CancellationToken 以支持异步取消
        /// </summary>
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.ClearLogs();
                _logger.LogInfo("日志已清空", "ClearLogsFeature.Handle");
                return new Result(true, "日志已清空");
            }
            catch (Exception ex)
            {
                _logger.LogError($"清空日志失败: {ex.Message}", "ClearLogsFeature.Handle");
                return new Result(false, $"清空失败: {ex.Message}");
            }
        }
    }
}
