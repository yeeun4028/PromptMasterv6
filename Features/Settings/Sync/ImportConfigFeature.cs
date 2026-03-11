using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;

namespace PromptMasterv6.Features.Settings.Sync
{
    public static class ImportConfigFeature
    {
        public record Command(string FilePath);
        public record Result(bool Success, string Message);

        public class Handler
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Result Handle(Command request)
            {
                try
                {
                    _settingsService.ImportSettings(request.FilePath);
                    
                    WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
                    
                    return new Result(true, "配置导入成功！");
                }
                catch (Exception ex)
                {
                    return new Result(false, $"配置导入失败: {ex.Message}");
                }
            }
        }
    }
}
