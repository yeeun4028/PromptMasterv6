using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Settings;

public static class GetAppConfigQuery
{
    // 1. 定义输入
    public record Query : IRequest<Result>;

    // 2. 定义输出
    public record Result(AppConfig Config);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Query, Result>
    {
        private readonly SettingsService _settingsService;

        public Handler(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Result(_settingsService.Config));
        }
    }
}
