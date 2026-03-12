using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Tray;

public static class PinToScreenFromClipboardFeature
{
    public record Command() : IRequest<Result>;

    public record Result(bool Success, string? ErrorMessage = null, bool HasImage = true);

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
                var success = _windowManager.ShowPinToScreenFromClipboard();
                if (!success)
                {
                    return Task.FromResult(new Result(false, "剪贴板中没有图片", false));
                }
                return Task.FromResult(new Result(true));
            }
            catch (System.Exception ex)
            {
                _logger.LogException(ex, "Pin to screen from clipboard failed", "PinToScreenFromClipboardFeature");
                return Task.FromResult(new Result(false, ex.Message));
            }
        }
    }
}
