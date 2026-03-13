using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Queries;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public static class SetCurrentFileFeature
{
    public record Command(
        PromptItem? File,
        bool EnterEditMode,
        ObservableCollection<VariableItem> Variables) : IRequest<Result>;

    public record Result(
        PromptItem? SelectedFile,
        bool IsEditMode,
        string? PreviewContent,
        bool HasVariables);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                request.Variables.Clear();
                return new Result(null, false, null, false);
            }

            var previewContent = await _mediator.Send(
                new ConvertHtmlToMarkdownQuery(request.File.Content),
                cancellationToken);

            var syncResult = await _mediator.Send(
                new SyncVariablesFeature.Command(request.File.Content, request.Variables),
                cancellationToken);

            return new Result(
                request.File,
                request.EnterEditMode,
                previewContent,
                syncResult.HasVariables);
        }
    }
}
