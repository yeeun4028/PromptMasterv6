using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync
{
    public static class ManualBackupFeature
    {
        public record Command() : IRequest<Result>;
        public record Result(bool Success, string Message);

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly IDataService _dataService;
            private readonly SettingsService _settingsService;
            private readonly ISessionState _sessionState;
            private readonly LoggerService _logger;

            public Handler(IDataService dataService, SettingsService settingsService, ISessionState sessionState, LoggerService logger)
            {
                _dataService = dataService;
                _settingsService = settingsService;
                _sessionState = sessionState;
                _logger = logger;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    await _dataService.SaveAsync(_sessionState.Folders, _sessionState.Files);
                    _sessionState.LocalConfig.LastCloudSyncTime = DateTime.Now;
                    _sessionState.IsDirty = false;
                    _sessionState.IsEditMode = false;
                    _settingsService.SaveLocalConfig();
                    
                    _logger.LogInfo("Manual cloud backup successful", "ManualBackupFeature.Handle");
                    return new Result(true, "云端备份成功！");
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to manual backup", "ManualBackupFeature.Handle");
                    return new Result(false, $"备份失败: {ex.Message}");
                }
            }
        }
    }
}
