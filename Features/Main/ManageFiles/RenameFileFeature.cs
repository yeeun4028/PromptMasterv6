using MediatR;
using PromptMasterv6.Features.Shared.Dialogs;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ManageFiles;

public static class RenameFileFeature
{
    public record Command(PromptItem? File) : IRequest<Result>;

    public record Result(bool Success);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Task.FromResult(new Result(false));
            }

            request.File.IsRenaming = true;
            return Task.FromResult(new Result(true));
        }
    }
}
