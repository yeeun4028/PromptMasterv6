using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class PerformLocalBackupFeature
{
    public record Command(
        ObservableCollection<FolderItem> Folders,
        ObservableCollection<PromptItem> Files) : IRequest;

    public class Handler : IRequestHandler<Command>
    {
        private readonly IDataService _localDataService;

        public Handler(
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService)
        {
            _localDataService = localDataService;
        }

        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            await _localDataService.SaveAsync(request.Folders, request.Files);
        }
    }
}
