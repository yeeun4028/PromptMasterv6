using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public static class SelectSubTabFeature
    {
        // 1. 定义输入 (必须实现 IRequest<Result>)
        public record Command(string TabIndexStr) : IRequest<Result>;

        // 2. 定义输出
        public record Result(bool Success, int SelectedTabIndex);

        // 3. 执行逻辑 (必须实现 IRequestHandler)
        public class Handler : IRequestHandler<Command, Result>
        {
            public Handler() { }

            // 必须带有 CancellationToken 以支持异步取消
            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                // 在这里实现从头到尾的纯粹业务逻辑。绝不能包含任何 UI 引用！
                if (int.TryParse(request.TabIndexStr, out int tabIndex))
                {
                    return new Result(true, tabIndex);
                }
                return new Result(false, 0);
            }
        }
    }
}
