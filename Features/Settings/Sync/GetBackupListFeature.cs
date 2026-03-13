using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Sync;

public static class GetBackupListFeature
{
    public record Query() : IRequest<Result>;

    public record Result(
        bool Success,
        List<BackupFileItem> Backups,
        string? BackupDirectory,
        string? ErrorMessage
    );

    public class Handler : IRequestHandler<Query, Result>
    {
        private readonly FileDataService _localDataService;
        private readonly LoggerService _logger;

        public Handler(FileDataService localDataService, LoggerService logger)
        {
            _localDataService = localDataService;
            _logger = logger;
        }

        public Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            try
            {
                var backups = _localDataService.GetBackups();
                _logger.LogInfo($"Found {backups.Count} backups in {_localDataService.BackupDirectory}", "GetBackupListFeature");
                
                return Task.FromResult(new Result(
                    Success: true,
                    Backups: backups,
                    BackupDirectory: _localDataService.BackupDirectory,
                    ErrorMessage: null
                ));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to get backup list", "GetBackupListFeature");
                return Task.FromResult(new Result(
                    Success: false,
                    Backups: new List<BackupFileItem>(),
                    BackupDirectory: _localDataService.BackupDirectory,
                    ErrorMessage: ex.Message
                ));
            }
        }
    }
}
