using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Features.Workspace.SyncVariables;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.SelectedFileChanged;

public static class SelectedFileChangedFeature
{
    public record Command(
        PromptItem? NewFile,
        ObservableCollection<VariableItem> Variables) : IRequest<Result>;

    public record Result(
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
            if (request.NewFile == null)
            {
                return new Result(null, false);
            }

            var previewContent = await _mediator.Send(
                new ConvertHtmlToMarkdownQuery(request.NewFile.Content), 
                cancellationToken);

            var varNames = await _mediator.Send(
                new ParseVariablesQuery(request.NewFile.Content), 
                cancellationToken);

            var syncResult = await _mediator.Send(
                new SyncVariablesFeature.Command(request.Variables, varNames), 
                cancellationToken);

            return new Result(previewContent, syncResult.HasVariables);
        }
    }
}
