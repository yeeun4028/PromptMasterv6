using MediatR;
using PromptMasterv6.Features.Shared.Dialogs;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class ChangeFolderIconFeature
{
    public record Command(FolderItem? Folder) : IRequest<Result>;

    public record Result(bool Success, string? NewIconGeometry);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Folder == null)
            {
                return Task.FromResult(new Result(false, null));
            }

            var dialog = new IconInputDialog(request.Folder.IconGeometry);
            if (dialog.ShowDialog() == true)
            {
                request.Folder.IconGeometry = dialog.ResultGeometry;
                return Task.FromResult(new Result(true, dialog.ResultGeometry));
            }

            return Task.FromResult(new Result(false, null));
        }
    }
}
