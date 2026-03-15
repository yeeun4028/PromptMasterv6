using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.FilterFiles;
using CommunityToolkit.Mvvm.Messaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.HandleFolderSelection;

public static class HandleFolderSelectionFeature
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
            await _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder), cancellationToken);
            _state.FilesView?.Refresh();

            if (_state.FilesView != null && !_state.FilesView.IsEmpty)
            {
                var firstItem = _state.FilesView.Cast<PromptItem>().FirstOrDefault();
                _state.SelectedFile = firstItem;
                WeakReferenceMessenger.Default.Send(new FileSelectedMessage(firstItem));
            }
            else
            {
                _state.SelectedFile = null;
            }

            return new Result(true);
        }
    }
}
