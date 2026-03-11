using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Core.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public static class SaveAiTranslationConfigFeature
    {
        public record Command(string PromptId, string PromptTitle, string BaseUrl, string ApiKey, string Model);
        public record Result(bool Success, string Message);

        public class Handler
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Result Handle(Command request)
            {
                var config = new AiTranslationConfig
                {
                    PromptId = request.PromptId,
                    PromptTitle = request.PromptTitle,
                    BaseUrl = request.BaseUrl,
                    ApiKey = request.ApiKey,
                    Model = request.Model
                };

                _settingsService.Config.SavedAiTranslationConfigs.Add(config);
                _settingsService.SaveConfig();
                
                return new Result(true, "AI 翻译配置已保存！");
            }
        }
    }
}
