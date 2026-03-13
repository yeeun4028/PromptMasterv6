using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.AiModels
{
    public static class TestAiConnectionFeature
    {
        /// <summary>
        /// 定义输入（必须实现 IRequest）
        /// </summary>
        public record Command(string ApiKey, string BaseUrl, string ModelName, bool UseProxy) : IRequest<Result>;

        /// <summary>
        /// 定义输出
        /// </summary>
        public record Result(bool Success, string Message, long? ResponseTimeMs);

        /// <summary>
        /// 执行逻辑（必须实现 IRequestHandler）
        /// </summary>
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly AiService _aiService;

            public Handler(AiService aiService)
            {
                _aiService = aiService;
            }

            /// <summary>
            /// 必须带有 CancellationToken 以支持异步取消
            /// </summary>
            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                var (success, message, responseTimeMs) = await _aiService.TestConnectionAsync(
                    request.ApiKey, 
                    request.BaseUrl, 
                    request.ModelName, 
                    request.UseProxy);

                return new Result(success, message, responseTimeMs);
            }
        }
    }
}
