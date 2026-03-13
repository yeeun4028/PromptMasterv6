using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Main.Tray;
using PromptMasterv6.Features.AppCore.SingleInstance;

namespace PromptMasterv6.Features.AppCore.Shutdown
{
    public static class CleanupApplicationFeature
    {
        public record Command(
            IServiceProvider ServiceProvider,
            System.Threading.Mutex? Mutex,
            bool OwnsMutex
        ) : IRequest<Result>;

        public record Result(
            bool Success,
            string Message
        );

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly LoggerService _logger;
            private readonly IMediator _mediator;

            public Handler(LoggerService logger, IMediator mediator)
            {
                _logger = logger;
                _mediator = mediator;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    _logger.LogInfo("Starting application cleanup...", "CleanupApplicationFeature");

                    var serviceProvider = request.ServiceProvider;

                    _logger.LogInfo("Disposing MainViewModel resources...", "CleanupApplicationFeature");
                    var mainVM = serviceProvider?.GetService(typeof(MainViewModel)) as MainViewModel;
                    if (mainVM != null)
                    {
                        mainVM.Dispose();
                        _logger.LogInfo("MainViewModel disposed successfully.", "CleanupApplicationFeature");
                    }

                    _logger.LogInfo("Cleaning up tray icon...", "CleanupApplicationFeature");
                    await _mediator.Send(
                        new CleanupTrayIconFeature.Command("ApplicationExit"),
                        cancellationToken);

                    _logger.LogInfo("Releasing single instance mutex...", "CleanupApplicationFeature");
                    await _mediator.Send(
                        new ReleaseSingleInstanceFeature.Command(
                            request.Mutex,
                            request.OwnsMutex),
                        cancellationToken);

                    _logger.LogInfo("Disposing service provider...", "CleanupApplicationFeature");
                    (serviceProvider as ServiceProvider)?.Dispose();

                    _logger.LogInfo("Application cleanup completed successfully.", "CleanupApplicationFeature");

                    return new Result(
                        Success: true,
                        Message: "应用程序清理成功"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to cleanup application", "CleanupApplicationFeature");
                    return new Result(
                        Success: false,
                        Message: $"应用程序清理失败: {ex.Message}"
                    );
                }
            }
        }
    }
}
