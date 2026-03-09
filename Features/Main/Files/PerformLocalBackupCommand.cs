using MediatR;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Files;

public record PerformLocalBackupCommand(
    ObservableCollection<FolderItem> Folders,
    ObservableCollection<PromptItem> Files) : IRequest;

public class PerformLocalBackupHandler : IRequestHandler<PerformLocalBackupCommand>
{
    private readonly IDataService _localDataService;

    public PerformLocalBackupHandler(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService)
    {
        _localDataService = localDataService;
    }

    public async Task Handle(PerformLocalBackupCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _localDataService.SaveAsync(request.Folders, request.Files);
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to perform local backup", "PerformLocalBackupHandler");
        }
    }
}
