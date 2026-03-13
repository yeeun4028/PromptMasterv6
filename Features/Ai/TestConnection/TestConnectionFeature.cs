using MediatR;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Ai.TestConnection;

public static class TestConnectionFeature
{
    public record Command(
        string ApiKey, 
        string BaseUrl, 
        string Model, 
        bool UseProxy = false) : IRequest<Result>;

    public record Result(bool Success, string Message, long? ResponseTimeMs);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly OpenAiServiceFactory _serviceFactory;
        private readonly LoggerService _logger;

        public Handler(OpenAiServiceFactory serviceFactory, LoggerService logger)
        {
            _serviceFactory = serviceFactory;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInfo($"Testing connection: BaseUrl={request.BaseUrl}, Model={request.Model}, UseProxy={request.UseProxy}", "TestConnectionFeature");
            
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                _logger.LogError("API Key is empty", "TestConnectionFeature");
                return new Result(false, "API Key 为空", null);
            }
            if (string.IsNullOrWhiteSpace(request.BaseUrl))
            {
                _logger.LogError("Base URL is empty", "TestConnectionFeature");
                return new Result(false, "API 地址为空", null);
            }
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                _logger.LogError("Model name is empty", "TestConnectionFeature");
                return new Result(false, "模型名称为空", null);
            }

            try
            {
                var openAiService = _serviceFactory.GetOrCreateService(
                    request.ApiKey, 
                    request.BaseUrl, 
                    request.UseProxy);
                    
                _logger.LogInfo($"OpenAiService created, sending test request...", "TestConnectionFeature");

                var completionRequest = new ChatCompletionCreateRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        ChatMessage.FromSystem("Test"),
                        ChatMessage.FromUser("Hi")
                    },
                    Model = request.Model,
                    MaxTokens = 5
                };

                var stopwatch = Stopwatch.StartNew();
                var completionResult = await openAiService.ChatCompletion
                    .CreateCompletion(completionRequest, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                stopwatch.Stop();

                _logger.LogInfo($"Response received: Successful={completionResult.Successful}, Error={completionResult.Error?.Message ?? "null"}", "TestConnectionFeature");

                if (completionResult.Successful)
                {
                    return new Result(true, "连接成功", stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    var errorMsg = completionResult.Error?.Message ?? "未知错误";
                    _logger.LogError($"API returned error: {errorMsg} (Type: {completionResult.Error?.Type}, Code: {completionResult.Error?.Code})", "TestConnectionFeature");
                    return new Result(false, $"连接失败: {errorMsg}", null);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogException(ex, $"Test connection exception: BaseUrl={request.BaseUrl}, Model={request.Model}", "TestConnectionFeature");
                return new Result(false, $"连接异常: {ex.Message}", null);
            }
        }
    }
}
