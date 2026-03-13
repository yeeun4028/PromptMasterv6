using MediatR;
using PromptMasterv6.Features.Shared.Commands;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public static class CopyCompiledTextFeature
{
    public record Command(
        PromptItem? File,
        ObservableCollection<VariableItem> Variables,
        string AdditionalInput) : IRequest<Result>;

    public record Result(bool Success, string? ErrorMessage);

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
                return new Result(false, "未选择文件");
            }

            var variablesDict = new Dictionary<string, string>();
            foreach (var v in request.Variables)
            {
                variablesDict[v.Name] = v.Value ?? "";
            }

            await _mediator.Send(new CopyCompiledTextCommand(
                request.File.Content,
                variablesDict,
                request.AdditionalInput));

            return new Result(true, null);
        }
    }
}
