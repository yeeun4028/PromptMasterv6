using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.FilterFiles;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.SetSelectedFolder;

public static class SetSelectedFolderFeature
{
    public record Command(FolderItem? Folder) : IRequest<Result>;
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
            _state.SelectedFolder = request.Folder;
            await _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder), cancellationToken);
            _state.FilesView?.Refresh();

            return new Result(true);
        }
    }
}
