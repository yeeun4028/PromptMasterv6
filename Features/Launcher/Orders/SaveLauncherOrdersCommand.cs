using MediatR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.Orders
{
    public record SaveLauncherOrdersCommand(Dictionary<string, int> Orders) : IRequest;

    public class SaveLauncherOrdersHandler : IRequestHandler<SaveLauncherOrdersCommand>
    {
        public Task Handle(SaveLauncherOrdersCommand request, CancellationToken cancellationToken)
        {
            if (request.Orders == null) return Task.CompletedTask;

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PromptMasterv6", "launcher_orders.json");
            
            var dir = Path.GetDirectoryName(appDataPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }

            var json = JsonSerializer.Serialize(request.Orders);
            File.WriteAllText(appDataPath, json);

            return Task.CompletedTask;
        }
    }
}
