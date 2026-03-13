using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ManageFiles;

public static class ChangeFileIconFeature
{
    public record Command(PromptItem? File) : IRequest<Result>;

    public record Result(bool Success, string? NewIconGeometry);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly DialogService _dialogService;

        public Handler(DialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Task.FromResult(new Result(false, null));
            }

            var resultGeometry = _dialogService.ShowIconInputDialog(request.File.IconGeometry);
            if (resultGeometry != null)
            {
                request.File.IconGeometry = resultGeometry;
                return Task.FromResult(new Result(true, resultGeometry));
            }

            return Task.FromResult(new Result(false, null));
        }
    }
}
