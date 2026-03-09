using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Shared.Behaviors
{
    public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
    {
        private readonly LoggerService _logger;

        public UnhandledExceptionBehavior(LoggerService logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                var requestName = typeof(TRequest).Name;
                
                _logger.LogException(ex, $"Unhandled Exception for Request {requestName}", "MediatR Pipeline");

                throw; 
            }
        }
    }
}
