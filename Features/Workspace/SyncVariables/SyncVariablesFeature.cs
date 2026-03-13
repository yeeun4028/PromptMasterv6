using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.SyncVariables;

public static class SyncVariablesFeature
{
    public record Command(
        ObservableCollection<VariableItem> CurrentVariables,
        List<string> NewVariableNames) : IRequest<Result>;

    public record Result(bool HasVariables);

    public class Handler : IRequestHandler<Command, Result>
    {
        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var currentVariables = request.CurrentVariables;
            var newNames = request.NewVariableNames;

            for (int i = currentVariables.Count - 1; i >= 0; i--)
            {
                if (!newNames.Contains(currentVariables[i].Name))
                {
                    currentVariables.RemoveAt(i);
                }
            }

            foreach (var name in newNames)
            {
                if (!currentVariables.Any(v => v.Name == name))
                {
                    currentVariables.Add(new VariableItem { Name = name });
                }
            }

            return Task.FromResult(new Result(currentVariables.Count > 0));
        }
    }
}
