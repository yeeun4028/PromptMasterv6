using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Launcher;

namespace PromptMasterv6.Features.AppCore.Initialization
{
    public static class InitializeApplicationFeature
    {
        public record Command() : IRequest<Result>;

        public record Result(
            bool Success,
            string Message,
            MainWindow? MainWindow,
            LaunchBarWindow? LaunchBarWindow
        );

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly ApplicationBootstrapper _bootstrapper;

            public Handler(ApplicationBootstrapper bootstrapper)
            {
                _bootstrapper = bootstrapper;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                var bootstrapResult = await _bootstrapper.InitializeAsync(cancellationToken);
                
                return new Result(
                    Success: bootstrapResult.Success,
                    Message: bootstrapResult.Message,
                    MainWindow: bootstrapResult.MainWindow,
                    LaunchBarWindow: bootstrapResult.LaunchBarWindow
                );
            }
        }
    }
}
