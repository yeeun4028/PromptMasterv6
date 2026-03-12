using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class DeleteFolderFeature
{
    public record Command(
        FolderItem? Folder,
        ObservableCollection<FolderItem> Folders,
        ObservableCollection<PromptItem> Files) : IRequest<Result>;

    public record Result(bool Success, bool WasSelected);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Folder == null)
            {
                return Task.FromResult(new Result(false, false));
            }

            var filesInFolder = request.Files
                .Where(f => f.FolderId == request.Folder.Id)
                .ToList();
            
            foreach (var file in filesInFolder) 
            {
                request.Files.Remove(file);
            }

            bool wasSelected = request.Folders.Contains(request.Folder);
            request.Folders.Remove(request.Folder);

            return Task.FromResult(new Result(true, wasSelected));
        }
    }
}
