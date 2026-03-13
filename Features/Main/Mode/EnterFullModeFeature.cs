using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PromptMasterv6.Features.Main.Mode;

public static class EnterFullModeFeature
{
    // 1. 定义输入
    public record Command : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, bool IsFullMode);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        public Handler()
        {
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Result(true, true));
        }
    }
}
