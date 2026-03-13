using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ReorderFolder;

public static class ReorderFolderFeature
{
    public record Command(ObservableCollection<FolderItem> Folders, int OldIndex, int NewIndex) : IRequest<Result>;
    
    public record Result(bool Success);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Handler()
        {
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.OldIndex < 0 || request.OldIndex >= request.Folders.Count)
                return Task.FromResult(new Result(false));

            if (request.NewIndex < 0 || request.NewIndex >= request.Folders.Count)
                return Task.FromResult(new Result(false));

            request.Folders.Move(request.OldIndex, request.NewIndex);
            
            return Task.FromResult(new Result(true));
        }
    }
}
