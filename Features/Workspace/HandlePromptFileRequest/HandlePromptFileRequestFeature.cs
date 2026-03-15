using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.Workspace.State;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.HandlePromptFileRequest;

public static class HandlePromptFileRequestFeature
{
    public record Command(string? PromptId, RequestPromptFileMessage Message) : IRequest<Result>;
    public record Result(bool Found, PromptItem? File);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IWorkspaceState _state;

        public Handler(IWorkspaceState state)
        {
            _state = state;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Message.HasReceivedResponse)
            {
                return Task.FromResult(new Result(false, null));
            }

            var file = _state.Files.FirstOrDefault(f => f.Id == request.PromptId);
            if (file != null)
            {
                request.Message.Reply(new PromptFileResponseMessage { File = file });
                return Task.FromResult(new Result(true, file));
            }

            return Task.FromResult(new Result(false, null));
        }
    }
}
