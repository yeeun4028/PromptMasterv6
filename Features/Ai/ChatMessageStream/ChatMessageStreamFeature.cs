using MediatR;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Ai.ChatMessageStream;

public static class ChatMessageStreamFeature
{
    public record Command(
        List<ChatMessage> Messages, 
        string ApiKey, 
        string BaseUrl, 
        string Model, 
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

            var messages = new List<ChatMessage>(request.Messages);
            var hasSystemMessage = messages.Any(m => m.Role == "system");
            if (!hasSystemMessage)
            {
                var defaultSystemMessage = ChatMessage.FromSystem(
                    "You are a helpful assistant. Output result directly without unnecessary conversational filler. IMPORTANT: Always answer in Simplified Chinese unless the user explicitly asks for another language.");
                messages.Insert(0, defaultSystemMessage);
                _logger.LogInfo("Added default system message (GLM requirement)", "ChatMessageStreamFeature");
            }

            if (request.BaseUrl.Contains("bigmodel.cn", System.StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInfo($"Using native HttpClient for GLM stream: Model={request.Model}", "ChatMessageStreamFeature");
                
                await foreach (var chunk in NativeStreamAsync(
                    messages, 
                    request.ApiKey, 
                    request.BaseUrl, 
                    request.Model, 
                    request.UseProxy, 
                    cancellationToken).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                yield break;
            }

            var openAiService = _serviceFactory.GetOrCreateService(
                request.ApiKey, 
                request.BaseUrl, 
                request.UseProxy);

            var completionRequest = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = request.Model,
                Temperature = 0.7f,
                MaxTokens = 4096,
                Stream = true
            };

            _logger.LogInfo($"Starting stream request: Model={request.Model}, BaseUrl={request.BaseUrl}, MessageCount={messages.Count}", "ChatMessageStreamFeature");

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
                        _logger.LogError($"Stream Error: {errorMsg}", "ChatMessageStreamFeature");
                        yield return new StreamChunk(errorMsg, true);
                    }
                    else
                    {
                        _logger.LogError("Stream Error: Unknown error (Successful=false, Error=null)", "ChatMessageStreamFeature");
                        yield return new StreamChunk("[AI 错误] 未知错误", true);
                    }
                }
            }
        }

        private async IAsyncEnumerable<StreamChunk> NativeStreamAsync(
            List<ChatMessage> messages, 
            string apiKey, 
            string baseUrl, 
            string model, 
            bool useProxy,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var url = baseUrl.TrimEnd('/') + "/chat/completions";
            _logger.LogInfo($"[GLM Native] Sending to: {url}, UseProxy={useProxy}", "ChatMessageStreamFeature");

            var requestBody = new
            {
                model = model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = true,
                temperature = 0.7,
                max_tokens = 4096
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            _logger.LogInfo($"[GLM Native] Request body: {jsonContent.Substring(0, System.Math.Min(200, jsonContent.Length))}...", "ChatMessageStreamFeature");

            var (response, httpError) = await _serviceFactory.SendNativeStreamRequestAsync(url, apiKey, jsonContent, useProxy, cancellationToken).ConfigureAwait(false);
            
            if (httpError != null)
            {
                yield return new StreamChunk(httpError, true);
                yield break;
            }

            if (response == null)
            {
                yield return new StreamChunk("[HTTP 错误] 无响应", true);
                yield break;
            }

            _logger.LogInfo($"[GLM Native] Response status: {response.StatusCode}", "ChatMessageStreamFeature");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError($"[GLM Native] Error response: {errorBody}", "ChatMessageStreamFeature");
                yield return new StreamChunk($"[AI 错误] HTTP {(int)response.StatusCode}: {errorBody}", true);
                response.Dispose();
                yield break;
            }

            using (response)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.StartsWith("data: ")) continue;

                    var data = line.Substring(6);
                    if (data == "[DONE]") break;

                    var (content, error) = OpenAiServiceFactory.ParseSseChunk(data, _logger);
                    if (error != null)
                    {
                        yield return new StreamChunk(error, true);
                        yield break;
                    }
                    if (content != null)
                    {
                        yield return new StreamChunk(content, false);
                    }
                }
            }
        }
    }
}
