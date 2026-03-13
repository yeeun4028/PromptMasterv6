using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ManageFiles;

public static class RenameFolderFeature
{
    public record Command(FolderItem? Folder) : IRequest<Result>;

    public record Result(bool Success, string? NewName);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly DialogService _dialogService;

        public Handler(DialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Folder == null)
            {
                return Task.FromResult(new Result(false, null));
            }

            var (confirmed, resultName) = _dialogService.ShowNameInputDialog(request.Folder.Name);
            if (confirmed && !string.IsNullOrWhiteSpace(resultName))
            {
                request.Folder.Name = resultName;
                return Task.FromResult(new Result(true, resultName));
            }

            return Task.FromResult(new Result(false, null));
        }
    }
}
