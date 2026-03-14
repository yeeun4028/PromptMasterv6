using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Features.Workspace.SyncVariables;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.FilePropertyChanged;

public static class FilePropertyChangedFeature
{
    public record Command(
        PromptItem ChangedItem,
        string PropertyName,
        PromptItem? SelectedFile,
        ObservableCollection<VariableItem> Variables) : IRequest<Result>;

    public record Result(
        bool ShouldSave,
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
            if (request.PropertyName == nameof(PromptItem.LastModified))
            {
                return new Result(false, false);
            }

            bool hasVariables = false;

            if (request.PropertyName == nameof(PromptItem.Content) && 
                request.ChangedItem == request.SelectedFile)
            {
                var varNames = await _mediator.Send(
                    new ParseVariablesQuery(request.SelectedFile?.Content), 
                    cancellationToken);

                var syncResult = await _mediator.Send(
                    new SyncVariablesFeature.Command(request.Variables, varNames), 
                    cancellationToken);

                hasVariables = syncResult.HasVariables;
            }

            return new Result(true, hasVariables);
        }
    }
}
