using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ChangeFolderIcon;

public static class ChangeFolderIconFeature
{
    public record Command(FolderItem? Folder) : IRequest<Result>;

    public record Result(bool Success, string? NewIconGeometry);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly DialogService _dialogService;

        public Handler(DialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Folder == null)
            {
                return Task.FromResult(new Result(false, null));
            }

            var resultGeometry = _dialogService.ShowIconInputDialog(request.Folder.IconGeometry);
            if (resultGeometry != null)
            {
                request.Folder.IconGeometry = resultGeometry;
                return Task.FromResult(new Result(true, resultGeometry));
            }

            return Task.FromResult(new Result(false, null));
        }
    }
}
