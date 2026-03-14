using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Infrastructure.MediatR;

public class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseRequest
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

            var responseType = typeof(TResponse);
            
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

            throw;
        }
    }
}
