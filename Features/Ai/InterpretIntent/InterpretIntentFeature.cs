using MediatR;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Ai.InterpretIntent;

public static class InterpretIntentFeature
{
    public record Command(
        string SystemPrompt, 
        string UserText, 
        string ApiKey, 
        string BaseUrl, 
        string Model, 
        bool UseProxy = false) : IRequest<Result>;

    public record Result(bool Success, string Intent);

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
                return new Result(false, "NONE");
            }

            var openAiService = _serviceFactory.GetOrCreateService(
                request.ApiKey, 
                request.BaseUrl, 
                request.UseProxy);

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(request.SystemPrompt),
                ChatMessage.FromUser(request.UserText)
            };

            var completionRequest = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = request.Model,
                Temperature = 0.1f
            };

            try
            {
                _logger.LogInfo($"[InterpretIntentFeature] Sending routing request to Model={request.Model}", "InterpretIntentFeature");
                var completionResult = await openAiService.ChatCompletion
                    .CreateCompletion(completionRequest, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (completionResult.Successful && completionResult.Choices != null)
                {
                    var choice = completionResult.Choices.FirstOrDefault();
                    if (choice?.Message != null && !string.IsNullOrWhiteSpace(choice.Message.Content))
                    {
                        return new Result(true, choice.Message.Content.Trim());
                    }
                }
                
                _logger.LogWarning("[InterpretIntentFeature] API returned empty or unsuccessful response.", "InterpretIntentFeature");
                return new Result(false, "NONE");
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"[InterpretIntentFeature] Exception: {ex.Message}", "InterpretIntentFeature");
                return new Result(false, "NONE");
            }
        }
    }
}
