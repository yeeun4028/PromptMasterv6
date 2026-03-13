using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class MoveFileToFolderFeature
{
    public record Command(PromptItem File, FolderItem TargetFolder) : IRequest<Result>;

    public record Result(
        bool Success,
        bool WasSelected
    );

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null || request.TargetFolder == null)
            {
                return Task.FromResult(new Result(false, false));
            }

            if (request.File.FolderId == request.TargetFolder.Id)
            {
                return Task.FromResult(new Result(false, false));
            }

            bool wasSelected = false;
            request.File.FolderId = request.TargetFolder.Id;

            return Task.FromResult(new Result(true, wasSelected));
        }
    }
}
