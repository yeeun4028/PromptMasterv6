using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Hotkeys;

public static class InitializeGlobalHotkeyFeature
{
    // 1. 定义输入
    public record Command(string LauncherHotkey) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? ErrorMessage = null);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly GlobalKeyService _keyService;
        private readonly LoggerService _logger;

        public Handler(GlobalKeyService keyService, LoggerService logger)
        {
            _keyService = keyService;
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                _keyService.LauncherHotkeyString = request.LauncherHotkey;
                _keyService.Start();
                return Task.FromResult(new Result(true));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to start GlobalKeyService", "InitializeGlobalHotkeyFeature.Handle");
                return Task.FromResult(new Result(false, ex.Message));
            }
        }
    }
}
