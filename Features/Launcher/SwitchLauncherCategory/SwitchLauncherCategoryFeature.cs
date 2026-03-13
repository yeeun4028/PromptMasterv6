using MediatR;
using PromptMasterv6.Features.Launcher.FilterLauncherItems;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.SwitchLauncherCategory;

public static class SwitchLauncherCategoryFeature
{
    public record Command(
        List<LauncherItem> AllItems,
        Dictionary<string, int> ItemOrders,
        string Category,
        bool IsSinglePageDisplay) : IRequest<Result>;

    public record Result(
        bool Success,
        List<LauncherItem>? Bookmarks,
        List<LauncherItem>? Applications,
        List<LauncherItem>? Tools,
        List<LauncherItem>? FilteredItems,
        string? ErrorMessage);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                var filterResult = await _mediator.Send(new FilterLauncherItemsFeature.Command(
                    request.AllItems,
                    request.ItemOrders,
                    request.Category,
                    request.IsSinglePageDisplay), cancellationToken);

                if (!filterResult.Success)
                {
                    return new Result(false, null, null, null, null, filterResult.ErrorMessage);
                }

                return new Result(
                    true,
                    filterResult.Bookmarks,
                    filterResult.Applications,
                    filterResult.Tools,
                    filterResult.FilteredItems,
                    null);
            }
            catch (Exception ex)
            {
                return new Result(false, null, null, null, null, ex.Message);
            }
        }
    }
}
