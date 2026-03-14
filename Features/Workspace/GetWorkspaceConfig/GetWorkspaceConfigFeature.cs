using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.GetWorkspaceConfig;

public static class GetWorkspaceConfigFeature
{
    public record Query : IRequest<Result>;

    public record Result(
        List<WebTarget> WebDirectTargets,
        string? DefaultWebTargetName);

    public class Handler : IRequestHandler<Query, Result>
    {
        private readonly SettingsService _settingsService;

        public Handler(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var config = _settingsService.Config;
            var targets = config.WebDirectTargets?.ToList() ?? new List<WebTarget>();
            return Task.FromResult(new Result(
                targets,
                config.DefaultWebTargetName));
        }
    }
}
