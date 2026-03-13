using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ToggleEditMode;

public static class ToggleEditModeFeature
{
    public record Command(
        PromptItem? SelectedFile,
        bool IsCurrentlyInEditMode,
        string? OriginalContentBeforeEdit) : IRequest<Result>;

    public record Result(
        bool NewEditMode,
        bool ShouldSave,
        DateTime? NewLastModified,
        string? OriginalContentBeforeEdit);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.SelectedFile == null)
            {
                return Task.FromResult(new Result(false, false, null, null));
            }

            if (request.IsCurrentlyInEditMode)
            {
                bool contentChanged = !string.Equals(
                    request.OriginalContentBeforeEdit,
                    request.SelectedFile.Content,
                    StringComparison.Ordinal);

                DateTime? newLastModified = null;
                if (contentChanged)
                {
                    newLastModified = DateTime.Now;
                }

                return Task.FromResult(new Result(
                    false,
                    contentChanged,
                    newLastModified,
                    null));
            }

            return Task.FromResult(new Result(
                true,
                false,
                null,
                request.SelectedFile.Content));
        }
    }
}
