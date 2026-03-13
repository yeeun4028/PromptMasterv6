using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.Generic;

namespace PromptMasterv6.Features.Workspace.SaveAppData;

public static class SaveAppDataFeature
{
    public record Command(
        IEnumerable<FolderItem> Folders,
        IEnumerable<PromptItem> Files
    ) : IRequest<Result>;

    public record Result(
        bool Success,
        string Message
    );

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
                if (request.Folders == null || request.Files == null)
                {
                    return new Result(false, "数据不能为空");
                }

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
