using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public static class AddLaunchBarItemFeature
    {
        public record Command(string ColorHex, LaunchBarActionType ActionType, string ActionTarget, string Label) : IRequest<Result>;
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
                _settingsService.Config.LaunchBarItems.Add(new LaunchBarItem
                {
                    ColorHex = request.ColorHex,
                    ActionType = request.ActionType,
                    ActionTarget = request.ActionTarget,
                    Label = request.Label
                });
                _settingsService.SaveConfig();
                
                return Task.FromResult(new Result(true));
            }
        }
    }
}
