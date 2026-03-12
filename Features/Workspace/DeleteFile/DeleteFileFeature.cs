using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.DeleteFile;

public static class DeleteFileFeature
{
    // 1. 定义输入
    public record Command(PromptItem File, ObservableCollection<PromptItem> Files) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, bool WasSelected, string? ErrorMessage);

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
                return Task.FromResult(new Result(false, false, "文件不能为空"));
            }

            if (request.Files == null)
            {
                return Task.FromResult(new Result(false, false, "文件集合不能为空"));
            }

            var wasSelected = request.Files.Contains(request.File);
            request.Files.Remove(request.File);

            return Task.FromResult(new Result(true, wasSelected, null));
        }
    }
}
