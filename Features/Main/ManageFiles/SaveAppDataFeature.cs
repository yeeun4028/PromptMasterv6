using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.Generic;

namespace PromptMasterv6.Features.Main.ManageFiles;

/// <summary>
/// 保存应用数据功能
/// </summary>
public static class SaveAppDataFeature
{
    // 1. 定义输入
    public record Command(
        IEnumerable<FolderItem> Folders,
        IEnumerable<PromptItem> Files
    ) : IRequest<Result>;

    // 2. 定义输出
    public record Result(
        bool Success,
        string Message
    );

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IDataService _dataService;
        private readonly LoggerService _logger;

        public Handler(
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
            LoggerService logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                // 验证数据
                if (request.Folders == null || request.Files == null)
                {
                    return new Result(false, "数据不能为空");
                }

                // 保存数据
                await _dataService.SaveAsync(request.Folders, request.Files, cancellationToken);

                _logger.LogInfo("应用数据保存成功", "SaveAppDataFeature.Handle");
                return new Result(true, "数据保存成功");
            }
            catch (Exception ex)
            {
                _logger.LogError($"数据保存失败: {ex.Message}", "SaveAppDataFeature.Handle");
                return new Result(false, $"数据保存失败: {ex.Message}");
            }
        }
    }
}
