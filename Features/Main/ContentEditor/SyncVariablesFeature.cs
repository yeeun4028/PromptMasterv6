using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public static class SyncVariablesFeature
{
    public record Command(string? Content, ObservableCollection<VariableItem> Variables) : IRequest<Result>;

    public record Result(bool HasVariables);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;

        public Handler(IMediator mediator, LoggerService logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Content))
            {
                request.Variables.Clear();
                return new Result(false);
            }

            try
            {
                var varNames = await _mediator.Send(new ParseVariablesQuery(request.Content), cancellationToken);

                for (int i = request.Variables.Count - 1; i >= 0; i--)
                {
                    if (!varNames.Contains(request.Variables[i].Name))
                    {
                        request.Variables.RemoveAt(i);
                    }
                }

                foreach (var name in varNames)
                {
                    if (!request.Variables.Any(v => v.Name == name))
                    {
                        request.Variables.Add(new VariableItem { Name = name });
                    }
                }

                return new Result(request.Variables.Count > 0);
            }
            catch (System.Exception ex)
            {
                _logger.LogException(ex, "变量解析失败", "SyncVariablesFeature");
                return new Result(request.Variables.Count > 0);
            }
        }
    }
}
