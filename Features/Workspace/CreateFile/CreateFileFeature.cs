using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CreateFile;

public static class CreateFileFeature
{
    public record Command(string FolderId) : IRequest<Result>;

    public record Result(PromptItem? CreatedFile);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.FolderId))
            {
                return Task.FromResult(new Result(null));
            }

            var file = new PromptItem
            {
                Title = "未命名提示词",
                Content = "",
                FolderId = request.FolderId,
                LastModified = DateTime.Now,
                IsRenaming = true
            };

            return Task.FromResult(new Result(file));
        }
    }
}
