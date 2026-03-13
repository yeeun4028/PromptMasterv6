using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.WindowManagement;

public static class OpenSettingsFeature
{
    // 1. 定义输入
    public record Command : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? ErrorMessage = null);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly WindowManager _windowManager;
        private readonly LoggerService _logger;

        public Handler(WindowManager windowManager, LoggerService logger)
        {
            _windowManager = windowManager;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _windowManager.ShowSettingsWindow();
                });
                return new Result(true);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to open settings window", "OpenSettingsFeature.Handle");
                return new Result(false, ex.Message);
            }
        }
    }
}
