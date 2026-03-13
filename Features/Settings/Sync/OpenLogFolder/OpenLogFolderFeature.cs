using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Sync.OpenLogFolder;

/// <summary>
/// 打开日志文件夹功能
/// </summary>
public static class OpenLogFolderFeature
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
                var logDir = _logger.GetLogDirectory();

                if (!Directory.Exists(logDir))
                {
                    _logger.LogWarning($"日志文件夹不存在: {logDir}", "OpenLogFolderFeature.Handle");
                    return new Result(false, "日志文件夹不存在");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = logDir,
                    UseShellExecute = true,
                    Verb = "open"
                });

                _logger.LogInfo($"已打开日志文件夹: {logDir}", "OpenLogFolderFeature.Handle");
                return new Result(true, "日志文件夹已打开");
            }
            catch (Exception ex)
            {
                _logger.LogError($"打开日志文件夹失败: {ex.Message}", "OpenLogFolderFeature.Handle");
                return new Result(false, $"打开失败: {ex.Message}");
            }
        }
    }
}
