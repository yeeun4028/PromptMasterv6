using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Files;

public record ChangeFileIconCommand(PromptItem File) : IRequest<bool>;

public class ChangeFileIconHandler : IRequestHandler<ChangeFileIconCommand, bool>
{
    public Task<bool> Handle(ChangeFileIconCommand request, CancellationToken cancellationToken)
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
