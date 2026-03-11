using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.ExternalTools.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System.Linq;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public static class SaveApiCredentialsFeature
    {
        public record Command(
            ApiProvider Provider,
            ServiceType ServiceType,
            string Name,
            string Key1,
            string Key2,
            string? BaseUrl = null);
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
                var profile = _settingsService.Config.ApiProfiles.FirstOrDefault(p =>
                    p.Provider == request.Provider && p.ServiceType == request.ServiceType);

                if (profile == null)
                {
                    profile = new ApiProfile
                    {
                        Name = request.Name,
                        Provider = request.Provider,
                        ServiceType = request.ServiceType
                    };
                    _settingsService.Config.ApiProfiles.Add(profile);
                }

                profile.Key1 = request.Key1 ?? "";
                profile.Key2 = request.Key2 ?? "";
                if (!string.IsNullOrEmpty(request.BaseUrl))
                {
                    profile.BaseUrl = request.BaseUrl;
                }

                if (request.ServiceType == ServiceType.OCR && string.IsNullOrEmpty(_settingsService.Config.OcrProfileId))
                {
                    _settingsService.Config.OcrProfileId = profile.Id;
                }
                if (request.ServiceType == ServiceType.Translation && string.IsNullOrEmpty(_settingsService.Config.TranslateProfileId))
                {
                    _settingsService.Config.TranslateProfileId = profile.Id;
                }

                _settingsService.SaveConfig();
                WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());

                return new Result(true);
            }
        }
    }
}
