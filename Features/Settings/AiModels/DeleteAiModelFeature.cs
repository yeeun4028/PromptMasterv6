using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Features.Settings.AiModels.Messages;

namespace PromptMasterv6.Features.Settings.AiModels
{
    public static class DeleteAiModelFeature
    {
        public record Command(AiModelConfig Model);

        public class Handler
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public void Handle(Command request)
            {
                var model = request.Model;
                var config = _settingsService.Config;

                var idx = config.SavedModels.IndexOf(model);
                if (idx >= 0)
                {
                    config.SavedModels.RemoveAt(idx);
                }

                if (config.ActiveModelId == model.Id)
                {
                    config.ActiveModelId = "";
                }

                _settingsService.SaveConfig();

                WeakReferenceMessenger.Default.Send(new AiModelDeletedMessage(model));
            }
        }
    }
}
