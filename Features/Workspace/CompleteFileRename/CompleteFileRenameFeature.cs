using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Events;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CompleteFileRename;

public static class CompleteFileRenameFeature
{
    public record Command(PromptItem File, bool ShouldSave = true) : IRequest<Result>;

    public record Result(bool Success, bool WasCancelled);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;
        private const string DefaultTitle = "未命名提示词";

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return new Result(false, false);
            }

            if (string.IsNullOrWhiteSpace(request.File.Title))
            {
                request.File.Title = DefaultTitle;
            }

            request.File.IsRenaming = false;

            if (request.ShouldSave)
            {
                await _mediator.Publish(new BackupActionRequestedEvent(), cancellationToken);
            }

            return new Result(true, false);
        }
    }
}
