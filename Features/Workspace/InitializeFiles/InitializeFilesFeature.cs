using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.FilterFiles;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PromptMasterv6.Features.Workspace.InitializeFiles;

public static class InitializeFilesFeature
{
    public record Command(
        ObservableCollection<PromptItem> Files,
        FolderItem? SelectedFolder,
        ICollectionView? FilesView) : IRequest<Result>;

    public record Result(
        ICollectionView? FilesView);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var filesView = request.FilesView ?? CollectionViewSource.GetDefaultView(request.Files);

            await _mediator.Send(new FilterFilesFeature.Command(filesView, request.SelectedFolder), cancellationToken);
            filesView.Refresh();

            return new Result(filesView);
        }
    }
}
