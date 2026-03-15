using MediatR;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.GetWorkspaceConfig;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.InitializeWorkspace;

public static class InitializeWorkspaceFeature
{
    public record Command() : IRequest<Result>;
    public record Result(bool Success);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IWorkspaceState _state;
        private readonly IMediator _mediator;

        public Handler(IWorkspaceState state, IMediator mediator)
        {
            _state = state;
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var config = await _mediator.Send(new GetWorkspaceConfigFeature.Query(), cancellationToken);
            _state.WebDirectTargets = config.WebDirectTargets;
            _state.DefaultWebTargetName = config.DefaultWebTargetName;

            return new Result(true);
        }
    }
}
