using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Tray;

public static class OpenSettingsFeature
{
    public record Command() : IRequest<Result>;

    public record Result(bool Success, string? ErrorMessage = null);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly WindowManager _windowManager;
        private readonly LoggerService _logger;

        public Handler(WindowManager windowManager, LoggerService logger)
        {
            _windowManager = windowManager;
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                _windowManager.ShowSettingsWindow();
                return Task.FromResult(new Result(true));
            }
            catch (System.Exception ex)
            {
                _logger.LogException(ex, "Failed to open settings", "OpenSettingsFeature");
                return Task.FromResult(new Result(false, ex.Message));
            }
        }
    }
}
