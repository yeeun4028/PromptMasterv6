using MediatR;
using PromptMasterv6.Features.Shared.Dialogs;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class RenameFolderFeature
{
    public record Command(FolderItem? Folder) : IRequest<Result>;

    public record Result(bool Success, string? NewName);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Folder == null)
            {
                return Task.FromResult(new Result(false, null));
            }

            var dialog = new NameInputDialog(request.Folder.Name);
            if (dialog.ShowDialog() == true)
            {
                request.Folder.Name = dialog.ResultName;
                return Task.FromResult(new Result(true, dialog.ResultName));
            }

            return Task.FromResult(new Result(false, null));
        }
    }
}
