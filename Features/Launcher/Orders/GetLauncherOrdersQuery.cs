using MediatR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.Orders
{
    public record GetLauncherOrdersQuery() : IRequest<Dictionary<string, int>>;

    public class GetLauncherOrdersHandler : IRequestHandler<GetLauncherOrdersQuery, Dictionary<string, int>>
    {
        public Task<Dictionary<string, int>> Handle(GetLauncherOrdersQuery request, CancellationToken cancellationToken)
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PromptMasterv6", "launcher_orders.json");

            if (File.Exists(appDataPath))
            {
                var json = File.ReadAllText(appDataPath);
                var result = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                return Task.FromResult(result);
            }

            return Task.FromResult(new Dictionary<string, int>());
        }
    }
}
