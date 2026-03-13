using MediatR;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Ai.Chat;

public static class ChatFeature
{
    public record Command(
        string UserContent, 
        string ApiKey, 
        string BaseUrl, 
        string Model, 
        string? SystemPrompt = null, 
        bool UseProxy = false) : IRequest<Result>;

    public record Result(bool Success, string Content, string? ErrorMessage);

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
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return new Result(false, "", "[设置错误] 请先在设置中配置 API Key");
            }

            var openAiService = _serviceFactory.GetOrCreateService(
                request.ApiKey, 
                request.BaseUrl, 
                request.UseProxy);

            string finalSystemPrompt = request.SystemPrompt 
                ?? "You are a helpful assistant. Output result directly without unnecessary conversational filler. IMPORTANT: Always answer in Simplified Chinese unless the user explicitly asks for another language.";

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(finalSystemPrompt),
                ChatMessage.FromUser(request.UserContent)
            };

            var completionRequest = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = request.Model,
                Temperature = 0.7f
            };

            try
            {
                _logger.LogInfo($"[ChatFeature] Sending request to Model={request.Model}, UseProxy={request.UseProxy}", "ChatFeature");
                
                var completionResult = await openAiService.ChatCompletion
                    .CreateCompletion(completionRequest, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (completionResult.Successful)
                {
                    var choice = completionResult.Choices?.FirstOrDefault();
                    if (choice != null && choice.Message != null && !string.IsNullOrEmpty(choice.Message.Content))
                    {
                        _logger.LogInfo($"[ChatFeature] Success! Received {choice.Message.Content.Length} chars.", "ChatFeature");
                        return new Result(true, choice.Message.Content.Trim(), null);
                    }
                    return new Result(false, "", "[AI 无响应] 返回内容为空");
                }
                else
                {
                    string errorMsg = completionResult.Error == null 
                        ? "[AI 错误] 未知网络错误" 
                        : $"[AI 错误] {completionResult.Error.Message} ({completionResult.Error.Type})";
                    
                    _logger.LogError($"[ChatFeature] API Error: {errorMsg}", "ChatFeature");
                    return new Result(false, "", errorMsg);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"[ChatFeature] Exception: {ex.Message}", "ChatFeature");
                return new Result(false, "", $"[系统错误] {ex.Message}");
            }
        }
    }
}
