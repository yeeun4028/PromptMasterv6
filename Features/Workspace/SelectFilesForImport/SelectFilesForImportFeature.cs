using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.SelectFilesForImport;

public static class SelectFilesForImportFeature
{
    public record Command(string Filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*") : IRequest<Result>;

    public record Result(
        bool Success,
        string[]? Files
    );

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly DialogService _dialogService;

        public Handler(DialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var files = _dialogService.ShowOpenFilesDialog(request.Filter);

            if (files == null || files.Length == 0)
            {
                return Task.FromResult(new Result(false, null));
            }

            return Task.FromResult(new Result(true, files));
        }
    }
}
