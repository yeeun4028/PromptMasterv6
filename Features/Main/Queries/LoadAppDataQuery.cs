using MediatR;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Queries;

public record LoadAppDataQuery() : IRequest<AppDataResult>;

public record AppDataResult
{
    public List<PromptItem> Files { get; init; } = new();
    public List<FolderItem> Folders { get; init; } = new();
    public FolderItem? DefaultFolder { get; init; }
}

public class LoadAppDataQueryHandler : IRequestHandler<LoadAppDataQuery, AppDataResult>
{
    private readonly IDataService _cloudDataService;
    private readonly IDataService _localDataService;

    public LoadAppDataQueryHandler(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService cloudDataService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService)
    {
        _cloudDataService = cloudDataService;
        _localDataService = localDataService;
    }

    public async Task<AppDataResult> Handle(LoadAppDataQuery request, CancellationToken cancellationToken)
    {
        AppData data;
        
        try
        {
            data = await _cloudDataService.LoadAsync();
        }
        catch
        {
            data = new AppData();
        }

        if ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0)
        {
            try
            {
                data = await _localDataService.LoadAsync();
            }
            catch
            {
                data = new AppData();
            }
        }

        var files = data.Files ?? new List<PromptItem>();
        var folders = data.Folders ?? new List<FolderItem>();

        FolderItem? defaultFolder = null;
        if (folders.Count == 0)
        {
            defaultFolder = new FolderItem { Name = "默认" };
            folders.Add(defaultFolder);
        }
        else
        {
            defaultFolder = folders.FirstOrDefault();
        }

        if (defaultFolder != null)
        {
            foreach (var f in files)
            {
                if (string.IsNullOrWhiteSpace(f.FolderId))
                {
                    f.FolderId = defaultFolder.Id;
                }
            }
        }

        return new AppDataResult
        {
            Files = files,
            Folders = folders,
            DefaultFolder = defaultFolder
        };
    }
}
