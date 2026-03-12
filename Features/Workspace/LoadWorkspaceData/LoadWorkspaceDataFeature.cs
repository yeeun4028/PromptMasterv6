using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.LoadWorkspaceData;

public static class LoadWorkspaceDataFeature
{
    // 1. 定义输入
    public record Command() : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, List<PromptItem>? Files, List<FolderItem>? Folders, string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IDataService _dataService;
        private readonly IDataService _localDataService;

        public Handler(
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService)
        {
            _dataService = dataService;
            _localDataService = localDataService;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            AppData? data = null;

            // 优先尝试云端数据
            try
            {
                data = await _dataService.LoadAsync();
            }
            catch
            {
                // 云端加载失败，继续尝试本地
            }

            // 如果云端数据为空，尝试本地数据
            if ((data?.Files?.Count ?? 0) == 0 && (data?.Folders?.Count ?? 0) == 0)
            {
                try
                {
                    data = await _localDataService.LoadAsync();
                }
                catch
                {
                    // 本地加载也失败
                }
            }

            if (data == null)
            {
                return new Result(false, null, null, "无法加载数据");
            }

            return new Result(true, data.Files ?? new List<PromptItem>(), data.Folders ?? new List<FolderItem>(), null);
        }
    }
}
