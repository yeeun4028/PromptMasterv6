using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public static class RemoveLaunchBarItemFeature
    {
        public record Command(LaunchBarItem Item) : IRequest<Result>;
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
                if (request.Item == null)
                {
                    return Task.FromResult(new Result(false));
                }

                _settingsService.Config.LaunchBarItems.Remove(request.Item);
                _settingsService.SaveConfig();
                
                return Task.FromResult(new Result(true));
            }
        }
    }
}
