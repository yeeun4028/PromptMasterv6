using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Settings;

public static class SaveLocalConfigFeature
{
    // 1. 定义输入
    public record Command : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? ErrorMessage = null);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;

        public Handler(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                _settingsService.SaveLocalConfig();
                return Task.FromResult(new Result(true));
            }
            catch (System.Exception ex)
            {
                return Task.FromResult(new Result(false, ex.Message));
            }
        }
    }
}
