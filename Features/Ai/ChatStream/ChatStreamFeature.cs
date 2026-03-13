using MediatR;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Ai.ChatStream;

public static class ChatStreamFeature
{
    public record Command(
        string UserContent, 
        string ApiKey, 
        string BaseUrl, 
        string Model, 
        string? SystemPrompt = null, 
        bool UseProxy = false) : IRequest<Result>;

    public record Result(System.Collections.Generic.IAsyncEnumerable<StreamChunk> Stream);

    public record StreamChunk(string Content, bool IsError);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly OpenAiServiceFactory _serviceFactory;
        private readonly LoggerService _logger;

        public Handler(OpenAiServiceFactory serviceFactory, LoggerService logger)
        {
            _serviceFactory = serviceFactory;
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Result(StreamAsync(request, cancellationToken)));
        }

        private async IAsyncEnumerable<StreamChunk> StreamAsync(
            Command request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                yield return new StreamChunk("[设置错误] 请先在设置中配置 API Key", true);
                yield break;
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
                Temperature = 0.7f,
                MaxTokens = 4096,
                Stream = true
            };

            _logger.LogInfo($"Starting stream request: Model={request.Model}, BaseUrl={request.BaseUrl}", "ChatStreamFeature");

            await foreach (var completionResult in openAiService.ChatCompletion
                .CreateCompletionAsStream(completionRequest, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                if (completionResult.Successful)
                {
                    var choice = completionResult.Choices?.FirstOrDefault();
                    if (choice != null && choice.Message != null && !string.IsNullOrEmpty(choice.Message.Content))
                    {
                        yield return new StreamChunk(choice.Message.Content, false);
                    }
                }
                else
                {
                    if (completionResult.Error != null)
                    {
                        string errorMessage = !string.IsNullOrWhiteSpace(completionResult.Error.Message) 
                            ? completionResult.Error.Message 
                            : "未知错误";
                        string errorType = !string.IsNullOrWhiteSpace(completionResult.Error.Type) 
                            ? completionResult.Error.Type 
                            : "Unknown";
                        string errorCode = !string.IsNullOrWhiteSpace(completionResult.Error.Code) 
                            ? completionResult.Error.Code 
                            : "N/A";
                        
                        var errorMsg = $"[AI 错误] {errorMessage} (Type: {errorType}, Code: {errorCode})";
                        _logger.LogError($"Stream Error: {errorMsg}", "ChatStreamFeature");
                        yield return new StreamChunk(errorMsg, true);
                    }
                    else
                    {
                        _logger.LogError("Stream Error: Unknown error (Successful=false, Error=null)", "ChatStreamFeature");
                        yield return new StreamChunk("[AI 错误] 未知错误", true);
                    }
                }
            }
        }
    }
}
