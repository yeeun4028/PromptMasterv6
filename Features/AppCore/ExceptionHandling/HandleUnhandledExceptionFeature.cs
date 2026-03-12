using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AppCore.ExceptionHandling;

/// <summary>
/// 处理未处理异常功能
/// 负责捕获并记录应用程序中未处理的异常
/// </summary>
public static class HandleUnhandledExceptionFeature
{
    // 1. 定义输入
    /// <summary>
    /// 处理异常命令
    /// </summary>
    /// <param name="Exception">异常对象</param>
    /// <param name="Source">异常来源</param>
    /// <param name="IsTerminating">是否导致程序终止</param>
    /// <param name="ShowMessageToUser">是否向用户显示消息</param>
    public record Command(
        Exception? Exception,
        string Source,
        bool IsTerminating = false,
        bool ShowMessageToUser = true) : IRequest<Result>;

    // 2. 定义输出
    /// <summary>
    /// 处理结果
    /// </summary>
    /// <param name="Success">是否成功处理</param>
    /// <param name="Message">结果消息</param>
    public record Result(bool Success, string Message);

    // 3. 执行逻辑
    /// <summary>
    /// 异常处理处理器
    /// </summary>
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly LoggerService _logger;

        /// <summary>
        /// 构造函数 - 只注入当前 Feature 绝对需要的服务
        /// </summary>
        public Handler(LoggerService logger)
        {
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Exception == null)
                {
                    _logger.LogError($"Non-exception object received from {request.Source}", "HandleUnhandledExceptionFeature.Handle");
                    return Task.FromResult(new Result(true, "非异常对象已记录"));
                }

                var logMessage = request.IsTerminating
                    ? $"Fatal unhandled exception from {request.Source}. Application is terminating."
                    : $"Unhandled exception from {request.Source}";

                _logger.LogException(request.Exception, logMessage, "HandleUnhandledExceptionFeature.Handle");

                if (request.ShowMessageToUser)
                {
                    var title = request.IsTerminating ? "致命错误" : "错误";
                    var message = request.IsTerminating
                        ? $"发生致命错误: {request.Exception.Message}"
                        : $"发生未处理异常: {request.Exception.Message}";

                    System.Windows.MessageBox.Show(message, title,
                        System.Windows.MessageBoxButton.OK,
                        request.IsTerminating ? System.Windows.MessageBoxImage.Error : System.Windows.MessageBoxImage.Warning);
                }

                return Task.FromResult(new Result(true, "异常已处理并记录"));
            }
            catch (Exception ex)
            {
                // 如果异常处理本身失败，尝试记录到调试输出
                System.Diagnostics.Debug.WriteLine($"Failed to handle exception: {ex.Message}");
                return Task.FromResult(new Result(false, $"异常处理失败: {ex.Message}"));
            }
        }
    }
}
