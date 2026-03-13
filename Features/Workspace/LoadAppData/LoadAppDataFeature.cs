using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Workspace.LoadAppData;

public static class LoadAppDataFeature
{
    public record Command() : IRequest<Result>;

    public record Result(
        bool Success,
        string Message,
        AppData? Data = null,
        bool UsedDefaultData = false
    );

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IDataService _dataService;
        private readonly IDataService _localDataService;
        private readonly LoggerService _logger;

        public Handler(
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService,
            LoggerService logger)
        {
            _dataService = dataService;
            _localDataService = localDataService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _dataService.LoadAsync(cancellationToken);

                if ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0)
                {
                    _logger.LogInfo("云端数据为空,尝试从本地加载", "LoadAppDataFeature.Handle");
                    
                    try
                    {
                        data = await _localDataService.LoadAsync(cancellationToken);
                    }
                    catch (Exception localEx)
                    {
                        _logger.LogWarning($"本地数据加载失败: {localEx.Message},使用默认数据", "LoadAppDataFeature.Handle");
                        data = CreateDefaultData();
                        return new Result(true, "已创建默认数据", data, true);
                    }
                }

                _logger.LogInfo("应用数据加载成功", "LoadAppDataFeature.Handle");
                return new Result(true, "数据加载成功", data);
            }
            catch (Exception ex)
            {
                _logger.LogError($"数据加载失败: {ex.Message}", "LoadAppDataFeature.Handle");
                
                try
                {
                    var localData = await _localDataService.LoadAsync(cancellationToken);
                    _logger.LogInfo("从本地加载数据成功", "LoadAppDataFeature.Handle");
                    return new Result(true, "从本地加载数据成功", localData);
                }
                catch (Exception localEx)
                {
                    _logger.LogError($"本地数据加载也失败: {localEx.Message},使用默认数据", "LoadAppDataFeature.Handle");
                    var defaultData = CreateDefaultData();
                    return new Result(true, "数据加载失败,已创建默认数据", defaultData, true);
                }
            }
        }

        private AppData CreateDefaultData()
        {
            return new AppData
            {
                Folders = new System.Collections.Generic.List<FolderItem>
                {
                    new FolderItem { Name = "默认" }
                },
                Files = new System.Collections.Generic.List<PromptItem>()
            };
        }
    }
}
