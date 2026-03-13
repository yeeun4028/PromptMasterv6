using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.AutoSave;

public static class ScheduleAutoSaveFeature
{
    // 1. 定义输入
    public record Command : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? ErrorMessage = null);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;

        public Handler(IMediator mediator, LoggerService logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                // 触发本地备份 - 通过 PerformLocalBackupFeature 执行
                // 这里不直接发送消息,而是通过 Feature 调用
                // 备份逻辑由 BackupViewModel 通过消息机制处理
                return new Result(true);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to schedule auto save", "ScheduleAutoSaveFeature.Handle");
                return new Result(false, ex.Message);
            }
        }
    }
}
