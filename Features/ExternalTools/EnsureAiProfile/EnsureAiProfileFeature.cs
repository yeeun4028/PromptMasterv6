using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.ExternalTools.EnsureAiProfile;

public static class EnsureAiProfileFeature
{
    // 1. 定义输入
    public record Command() : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, bool WasAdded, string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

        public Handler(
            SettingsService settingsService,
            LoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                bool added = false;
                var config = _settingsService.Config;

                // 确保 AI 翻译 Profile 存在
                if (!config.ApiProfiles.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.Translation))
                {
                    config.ApiProfiles.Add(new ApiProfile
                    {
                        Name = "AI 智能翻译",
                        Provider = ApiProvider.AI,
                        ServiceType = ServiceType.Translation,
                        Id = Guid.NewGuid().ToString()
                    });
                    added = true;
                }

                // 确保 AI OCR Profile 存在
                if (!config.ApiProfiles.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.OCR))
                {
                    config.ApiProfiles.Add(new ApiProfile
                    {
                        Name = "AI 智能 OCR",
                        Provider = ApiProvider.AI,
                        ServiceType = ServiceType.OCR,
                        Id = Guid.NewGuid().ToString()
                    });
                    added = true;
                }

                if (added)
                {
                    _settingsService.SaveConfig();
                    _logger.LogInfo("AI Profiles added to configuration", "EnsureAiProfileFeature");
                }

                return Task.FromResult(new Result(true, added, null));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to ensure AI profiles", "EnsureAiProfileFeature");
                return Task.FromResult(new Result(false, false, ex.Message));
            }
        }
    }
}
