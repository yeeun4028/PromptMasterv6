using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.AutoSave;

public static class ScheduleAutoSaveFeature
{
    // 定义保存类型枚举
    public enum SaveType
    {
        LocalSettings,
        DataBackup
    }

    // 1. 定义输入
    public record Command(SaveType Type, TimeSpan ThrottleInterval) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success);

    // 3. 执行逻辑
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
            try
            {
                await Task.Delay(request.ThrottleInterval, cancellationToken);

                switch (request.Type)
                {
                    case SaveType.LocalSettings:
                        _settingsService.SaveLocalConfig();
                        break;
                    case SaveType.DataBackup:
                        WeakReferenceMessenger.Default.Send(new RequestLocalBackupMessage());
                        break;
                }

                return new Result(true);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to schedule auto save", "ScheduleAutoSaveFeature.Handle");
                return new Result(false);
            }
        }
    }
}
