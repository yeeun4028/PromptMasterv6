using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Sidebar;

public static class ChangeActionIconFeature
{
    public record Command(string ActionKey, string NewIconGeometry) : IRequest<Result>;
    
    public record Result(bool Success, string? ErrorMessage);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

        public Handler(SettingsService settingsService, LoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ActionKey))
            {
                return new Result(false, "ActionKey 不能为空");
            }

            try
            {
                var localConfig = _settingsService.LocalConfig;
                
                if (localConfig.ActionIcons == null)
                {
                    localConfig.ActionIcons = new System.Collections.Generic.Dictionary<string, string>();
                }

                localConfig.ActionIcons[request.ActionKey] = request.NewIconGeometry ?? "";
                
                _settingsService.SaveLocalConfig();
                
                _logger.LogInfo($"Action icon updated: {request.ActionKey}", "ChangeActionIconFeature");
                
                return new Result(true, null);
            }
            catch (System.Exception ex)
            {
                _logger.LogException(ex, $"Failed to change action icon: {request.ActionKey}", "ChangeActionIconFeature");
                return new Result(false, ex.Message);
            }
        }
    }
}
