using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class DeleteFileFeature
{
    public record Command(
        PromptItem? File,
        ObservableCollection<PromptItem> Files) : IRequest<Result>;

    public record Result(bool Success, bool WasSelected);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Task.FromResult(new Result(false, false));
            }

            bool wasSelected = request.Files.Contains(request.File);
            request.Files.Remove(request.File);

            return Task.FromResult(new Result(true, wasSelected));
        }
    }
}
