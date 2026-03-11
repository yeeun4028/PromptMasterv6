using PromptMasterv6.Infrastructure.Services;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.AiModels
{
    public static class TestAiConnectionFeature
    {
        public record Command(string ApiKey, string BaseUrl, string ModelName, bool UseProxy);

        public record Result(bool Success, string Message, long? ResponseTimeMs);

        public class Handler
        {
            private readonly AiService _aiService;

            public Handler(AiService aiService)
            {
                _aiService = aiService;
            }

            public async Task<Result> Handle(Command request)
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
