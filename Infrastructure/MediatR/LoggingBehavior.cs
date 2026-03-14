
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Infrastructure.MediatR;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseRequest
{
    private readonly LoggerService _logger;

    public LoggingBehavior(LoggerService logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInfo($"开始执行: {requestName}", "LoggingBehavior.Handle");

        try
        {
            var response = await next();
            
            stopwatch.Stop();
            _logger.LogInfo($"执行完成: {requestName}, 耗时: {stopwatch.ElapsedMilliseconds}ms", "LoggingBehavior.Handle");

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError($"执行失败: {requestName}, 耗时: {stopwatch.ElapsedMilliseconds}ms, 错误: {ex.Message}", "LoggingBehavior.Handle");
            throw;
        }
    }
}
