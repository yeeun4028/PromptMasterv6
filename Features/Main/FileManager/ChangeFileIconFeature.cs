using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Dialogs;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class ChangeFileIconFeature
{
    public record Command(PromptItem File) : IRequest<bool>;

    public class Handler : IRequestHandler<Command, bool>
    {
        public Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var dialog = new IconInputDialog(request.File.IconGeometry);
            if (dialog.ShowDialog() == true)
            {
                request.File.IconGeometry = dialog.ResultGeometry;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
