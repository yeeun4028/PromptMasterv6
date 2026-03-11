using PromptMasterv6.Infrastructure.Services;
using System;

namespace PromptMasterv6.Features.Settings.Sync
{
    public static class ExportConfigFeature
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
                    _settingsService.ExportSettings(request.FilePath);
                    return new Result(true, "配置导出成功！");
                }
                catch (Exception ex)
                {
                    return new Result(false, $"配置导出失败: {ex.Message}");
                }
            }
        }
    }
}
