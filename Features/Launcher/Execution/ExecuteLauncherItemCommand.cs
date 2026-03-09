using MediatR;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.Execution
{
    public record ExecuteLauncherItemCommand(LauncherItem Item, bool RunAsAdmin) : IRequest;

    public class ExecuteLauncherItemHandler : IRequestHandler<ExecuteLauncherItemCommand>
    {
        public Task Handle(ExecuteLauncherItemCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Item?.Action != null)
                {
                    request.Item.Action.Invoke();
                    return Task.CompletedTask;
                }

                if (!string.IsNullOrEmpty(request.Item?.FilePath))
                {
                    var info = new ProcessStartInfo(request.Item.FilePath) 
                    { 
                        UseShellExecute = true 
                    };
                    
                    if (request.RunAsAdmin)
                    {
                        info.Verb = "runas";
                    }

                    Process.Start(info);
                }
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to execute launcher item", "ExecuteLauncherItemHandler");
            }
            
            return Task.CompletedTask;
        }
    }
}
