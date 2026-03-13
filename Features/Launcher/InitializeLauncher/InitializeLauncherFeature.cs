using MediatR;
using PromptMasterv6.Features.Launcher.Orders;
using PromptMasterv6.Features.Launcher.Queries;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.InitializeLauncher;

public static class InitializeLauncherFeature
{
    public record Command : IRequest<Result>;

    public record Result(
        bool Success,
        List<LauncherItem>? Items,
        Dictionary<string, int>? ItemOrders,
        string? ErrorMessage);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;
        private readonly IMediator _mediator;

        public Handler(SettingsService settingsService, IMediator mediator)
        {
            _settingsService = settingsService;
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                var itemOrders = await _mediator.Send(new GetLauncherOrdersQuery(), cancellationToken);

                var paths = _settingsService.Config.LauncherSearchPaths;
                if (paths == null || !paths.Any())
                {
                    return new Result(true, new List<LauncherItem>(), itemOrders, null);
                }

                var discovered = await _mediator.Send(new GetLauncherItemsQuery(paths), cancellationToken);

                return new Result(true, discovered, itemOrders, null);
            }
            catch (Exception ex)
            {
                return new Result(false, null, null, ex.Message);
            }
        }
    }
}
