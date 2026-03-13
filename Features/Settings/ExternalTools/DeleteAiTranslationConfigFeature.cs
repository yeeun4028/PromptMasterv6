using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public static class DeleteAiTranslationConfigFeature
    {
        public record Command(string ConfigId) : IRequest<Result>;
        public record Result(bool Success);

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(request.ConfigId))
                {
                    return Task.FromResult(new Result(false));
                }

                var config = _settingsService.Config.SavedAiTranslationConfigs
                    .FirstOrDefault(c => c.Id == request.ConfigId);
                    
                if (config != null)
                {
                    _settingsService.Config.SavedAiTranslationConfigs.Remove(config);
                    _settingsService.SaveConfig();
                    return Task.FromResult(new Result(true));
                }

                return Task.FromResult(new Result(false));
            }
        }
    }
}
