using MediatR;
using PromptMasterv6.Infrastructure.Services;
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
        private readonly DialogService _dialogService;

        public Handler(DialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return Task.FromResult(new Result(false, null, "文件不能为空"));
            }

            var resultGeometry = _dialogService.ShowIconInputDialog(request.File.IconGeometry);
            if (resultGeometry != null)
            {
                return Task.FromResult(new Result(true, resultGeometry, null));
            }

            return Task.FromResult(new Result(false, null, null)); // 用户取消
        }
    }
}
