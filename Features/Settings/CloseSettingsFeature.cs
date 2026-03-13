using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings;

public static class CloseSettingsFeature
{
    public record Command() : IRequest<Result>;

    public record Result(bool Success, string Message);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;

        public Handler(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _settingsService.SaveConfig();
            _settingsService.SaveLocalConfig();
            return Task.FromResult(new Result(true, "设置已保存"));
        }
    }
}
