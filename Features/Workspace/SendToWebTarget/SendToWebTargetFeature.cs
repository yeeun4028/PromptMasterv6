using MediatR;
using PromptMasterv6.Features.Shared.Commands;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Queries;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.SendToWebTarget;

public static class SendToWebTargetFeature
{
    public record Command(
        PromptItem? SelectedFile,
        ObservableCollection<VariableItem> Variables,
        bool HasVariables,
        string AdditionalInput,
        WebTarget? Target = null,
        ObservableCollection<WebTarget>? AllTargets = null,
        string? DefaultTargetName = null) : IRequest<Result>;

    public record Result(bool Success, string? ErrorMessage, bool ShouldClearAdditionalInput);

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
                return new Result(false, "未选择文件", false);
            }

            if (request.HasVariables)
            {
                var emptyVariables = request.Variables
                    .Where(v => string.IsNullOrWhiteSpace(v.Value))
                    .ToList();

                if (emptyVariables.Any())
                {
                    return new Result(false, "请先填写所有变量值。", false);
                }
            }

            var variablesDict = request.Variables
                .ToDictionary(v => v.Name, v => v.Value ?? "");

            var content = await _mediator.Send(new CompileContentQuery(
                request.SelectedFile.Content,
                variablesDict,
                request.AdditionalInput));

            if (request.Target != null)
            {
                await _mediator.Send(new ExecuteWebTargetCommand(request.Target, content));
            }
            else if (request.AllTargets != null && !string.IsNullOrEmpty(request.DefaultTargetName))
            {
                await _mediator.Send(new SendToDefaultTargetCommand(
                    content,
                    request.AllTargets,
                    request.DefaultTargetName));
            }

            return new Result(true, null, true);
        }
    }
}
