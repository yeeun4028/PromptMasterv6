using MediatR;
using PromptMasterv6.Features.Shared.Dialogs;
using PromptMasterv6.Features.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ChangeFileIcon;

public static class ChangeFileIconFeature
{
    // 1. 定义输入
    public record Command(PromptItem File) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? NewIconGeometry, string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        public Handler()
        {
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Task.FromResult(new Result(false, null, "文件不能为空"));
            }

            var dialog = new IconInputDialog(request.File.IconGeometry);
            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult(new Result(true, dialog.ResultGeometry, null));
            }

            return Task.FromResult(new Result(false, null, null)); // 用户取消
        }
    }
}
