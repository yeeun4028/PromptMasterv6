using MediatR;
using PromptMasterv6.Features.Main.ContentEditor.Messages;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Queries;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public static class ToggleEditModeFeature
{
    public record Command(
        PromptItem? SelectedFile,
        bool CurrentEditMode,
        string? OriginalContentBeforeEdit) : IRequest<Result>;

    public record Result(
        bool NewEditMode,
        bool ShouldTriggerBackup,
        string? PreviewContent);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.SelectedFile == null)
            {
                return new Result(false, false, null);
            }

            if (request.CurrentEditMode)
            {
                bool contentChanged = !string.Equals(
                    request.OriginalContentBeforeEdit,
                    request.SelectedFile.Content,
                    StringComparison.Ordinal);

                if (contentChanged)
                {
                    request.SelectedFile.LastModified = DateTime.Now;
                    WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
                }

                var previewContent = await _mediator.Send(
                    new ConvertHtmlToMarkdownQuery(request.SelectedFile.Content),
                    cancellationToken);

                return new Result(false, contentChanged, previewContent);
            }

            return new Result(true, false, null);
        }
    }
}
