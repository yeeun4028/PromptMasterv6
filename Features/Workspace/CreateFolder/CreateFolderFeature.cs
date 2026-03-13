using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CreateFolder;

public static class CreateFolderFeature
{
    public record Command(ObservableCollection<FolderItem> Folders) : IRequest<Result>;

    public record Result(FolderItem? CreatedFolder);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var folder = new FolderItem 
            { 
                Name = $"新建文件夹 {request.Folders.Count + 1}" 
            };
            request.Folders.Add(folder);
            return Task.FromResult(new Result(folder));
        }
    }
}
