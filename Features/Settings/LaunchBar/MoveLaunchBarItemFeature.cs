using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public static class MoveLaunchBarItemFeature
    {
        public record Command(LaunchBarItem Item, MoveDirection Direction) : IRequest<Result>;
        public record Result(bool Success);

        public enum MoveDirection
        {
            Up,
            Down
        }

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

                var items = _settingsService.Config.LaunchBarItems;
                int index = items.IndexOf(request.Item);

                if (index < 0)
                {
                    return Task.FromResult(new Result(false));
                }

                int newIndex = request.Direction == MoveDirection.Up ? index - 1 : index + 1;

                if (newIndex < 0 || newIndex >= items.Count)
                {
                    return Task.FromResult(new Result(false));
                }

                items.Move(index, newIndex);
                _settingsService.SaveConfig();
                
                return Task.FromResult(new Result(true));
            }
        }
    }
}
