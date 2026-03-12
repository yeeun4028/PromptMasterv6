using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Sync.ClearLogs;

/// <summary>
/// 清空日志功能
/// </summary>
public static class ClearLogsFeature
{
    /// <summary>
    /// 定义输入
    /// </summary>
    public record Command();

    /// <summary>
    /// 定义输出
    /// </summary>
    public record Result(bool Success, string Message);

    /// <summary>
    /// 执行逻辑
    /// </summary>
    public class Handler
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
        /// 在这里实现从头到尾的业务逻辑
        /// </summary>
        public async Task<Result> Handle(Command request)
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
