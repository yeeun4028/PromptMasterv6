using MediatR;
using PromptMasterv6.Features.Shared.Commands;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public static class OpenWebTargetFeature
{
    public record Command(
        PromptItem? File,
        ObservableCollection<VariableItem> Variables,
        string AdditionalInput,
        WebTarget? Target) : IRequest<Result>;

    public record Result(bool Success, string? ErrorMessage);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;
        private readonly DialogService _dialogService;

        public Handler(
            IMediator mediator,
            DialogService dialogService)
        {
            _mediator = mediator;
            _dialogService = dialogService;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.File == null)
            {
                return new Result(false, "未选择文件");
            }

            if (request.Target == null)
            {
                return new Result(false, "未选择目标");
            }

            if (request.Variables != null)
            {
                foreach (var v in request.Variables)
                {
                    if (string.IsNullOrWhiteSpace(v.Value))
                    {
                        _dialogService.ShowAlert("请先填写所有变量值。", "变量未填");
                        return new Result(false, "变量未填写完整");
                    }
                }
            }

            var variablesDict = request.Variables?
                .ToDictionary(v => v.Name, v => v.Value ?? "") 
                ?? new Dictionary<string, string>();

            var content = await _mediator.Send(new CompileContentQuery(
                request.File.Content,
                variablesDict,
                request.AdditionalInput));

            await _mediator.Send(new ExecuteWebTargetCommand(request.Target, content));

            return new Result(true, null);
        }
    }
}
