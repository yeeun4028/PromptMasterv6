using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Infrastructure.MediatR;

/// <summary>
/// MediatR 异常处理管道行为
/// </summary>
public class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly LoggerService _logger;

    public ExceptionHandlingBehavior(LoggerService logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Feature 执行异常: {typeof(TRequest).Name}, 错误: {ex.Message}", "ExceptionHandlingBehavior.Handle");
            _logger.LogException(ex, "Feature execution failed", "ExceptionHandlingBehavior.Handle");

            // 尝试返回失败 Result
            var responseType = typeof(TResponse);
            
            // 检查是否是 Result 类型
            if (responseType.Name == "Result" || 
                (responseType.IsGenericType && responseType.GetGenericTypeDefinition().Name.StartsWith("Result")))
            {
                var successProperty = responseType.GetProperty("Success");
                var messageProperty = responseType.GetProperty("Message");

                if (successProperty != null && messageProperty != null)
                {
                    var result = Activator.CreateInstance<TResponse>();
                    successProperty.SetValue(result, false);
                    messageProperty.SetValue(result, "操作失败,请重试");
                    return result;
                }
            }

            // 如果无法创建失败 Result,重新抛出异常
            throw;
        }
    }
}
