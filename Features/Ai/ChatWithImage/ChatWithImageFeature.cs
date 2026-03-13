using MediatR;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Ai.ChatWithImage;

public static class ChatWithImageFeature
{
    public record Command(
        byte[] ImageBytes, 
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
                return new Result(false, "", "[设置错误] 请先配置 API Key");
            
            if (request.ImageBytes == null || request.ImageBytes.Length == 0) 
                return new Result(false, "", "[输入错误] 图片数据为空");

            var openAiService = _serviceFactory.GetOrCreateService(
                request.ApiKey, 
                request.BaseUrl, 
                request.UseProxy);

            string finalSystemPrompt = request.SystemPrompt ?? "You are a helpful assistant. Please perform OCR on the provided image.";
            string base64Image = System.Convert.ToBase64String(request.ImageBytes);
            string imageUrl = $"data:image/jpeg;base64,{base64Image}";

            string userPrompt = request.SystemPrompt == null 
                ? @"You are a highly precise OCR engine. Your ONLY objective is to extract text from the provided image.

STRICT RULES:
1. Output ONLY the extracted text. Absolutely NO introductory phrases, NO conversational filler (e.g., do not say 'Here is the text' or 'The image contains').
2. Preserve the exact original formatting, line breaks, indentations, and punctuation visible in the image.
3. If there are lists, tables, or code blocks, maintain their structural representation as closely as possible in Markdown format.
4. If no text is found, output exactly nothing (an empty string).
5. Do not explain your output. Do not add markdown code block wrappers (like ```) unless the original text itself is code.

Begin extraction now:"
                : "Please process this image according to the system instructions.";

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(finalSystemPrompt),
                ChatMessage.FromUser(
                    new List<MessageContent>
                    {
                        MessageContent.ImageUrlContent(imageUrl),
                        MessageContent.TextContent(userPrompt)
                    })
            };

            var completionRequest = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = request.Model,
                Temperature = 0.3f,
                MaxTokens = 2000
            };

            try
            {
                var completionResult = await openAiService.ChatCompletion
                    .CreateCompletion(completionRequest, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                    
                if (completionResult.Successful)
                {
                    var choice = completionResult.Choices?.FirstOrDefault();
                    if (choice != null && choice.Message != null && !string.IsNullOrEmpty(choice.Message.Content))
                    {
                        return new Result(true, choice.Message.Content.Trim(), null);
                    }
                    return new Result(false, "", "[AI 无响应] 返回内容为空");
                }
                else
                {
                    if (completionResult.Error == null) 
                        return new Result(false, "", "[AI 错误] 未知网络错误");
                    return new Result(false, "", $"[AI 错误] {completionResult.Error.Message} ({completionResult.Error.Type})");
                }
            }
            catch (System.Exception ex)
            {
                return new Result(false, "", $"[系统错误] {ex.Message}");
            }
        }
    }
}
