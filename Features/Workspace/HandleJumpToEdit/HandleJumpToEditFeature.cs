using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Workspace.State;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.HandleJumpToEdit;

public static class HandleJumpToEditFeature
{
    public record Command(PromptItem? File) : IRequest<Result>;
    public record Result(bool Success);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IWorkspaceState _state;

        public Handler(IWorkspaceState state)
        {
            _state = state;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Task.FromResult(new Result(false));
            }

            _state.SelectedFile = request.File;
            _state.IsEditMode = true;
            WeakReferenceMessenger.Default.Send(new FileSelectedMessage(request.File, true));

            return Task.FromResult(new Result(true));
        }
    }
}
