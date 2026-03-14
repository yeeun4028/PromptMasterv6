using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CancelFileRename;

public static class CancelFileRenameFeature
{
    public record Command(PromptItem File) : IRequest<Result>;

    public record Result(bool Success);

    public class Handler : IRequestHandler<Command, Result>
    {
        private const string DefaultTitle = "未命名提示词";

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Task.FromResult(new Result(false));
            }

            if (string.IsNullOrWhiteSpace(request.File.Title))
            {
                request.File.Title = DefaultTitle;
            }

            request.File.IsRenaming = false;

            return Task.FromResult(new Result(true));
        }
    }
}
