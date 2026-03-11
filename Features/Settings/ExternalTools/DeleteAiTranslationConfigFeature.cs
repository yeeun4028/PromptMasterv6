using PromptMasterv6.Infrastructure.Services;
using System.Linq;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public static class DeleteAiTranslationConfigFeature
    {
        public record Command(string ConfigId);
        public record Result(bool Success);

        public class Handler
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Result Handle(Command request)
            {
                if (string.IsNullOrWhiteSpace(request.ConfigId))
                {
                    return new Result(false);
                }

                var config = _settingsService.Config.SavedAiTranslationConfigs
                    .FirstOrDefault(c => c.Id == request.ConfigId);
                    
                if (config != null)
                {
                    _settingsService.Config.SavedAiTranslationConfigs.Remove(config);
                    _settingsService.SaveConfig();
                    return new Result(true);
                }

                return new Result(false);
            }
        }
    }
}
