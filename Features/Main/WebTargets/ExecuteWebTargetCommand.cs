using MediatR;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.WebTargets;

public record ExecuteWebTargetCommand(
    WebTarget? Target,
    PromptItem? SelectedFile,
    ObservableCollection<VariableItem> Variables,
    string AdditionalInput,
    AppConfig Config,
    bool IsDefaultTarget = false) : IRequest;

public class ExecuteWebTargetHandler : IRequestHandler<ExecuteWebTargetCommand>
{
    private readonly IVariableService _variableService;
    private readonly IWebTargetService _webTargetService;
    private readonly IDialogService _dialogService;

    public ExecuteWebTargetHandler(
        IVariableService variableService,
        IWebTargetService webTargetService,
        IDialogService dialogService)
    {
        _variableService = variableService;
        _webTargetService = webTargetService;
        _dialogService = dialogService;
    }

    public async Task Handle(ExecuteWebTargetCommand request, CancellationToken cancellationToken)
    {
        if (request.SelectedFile == null && request.Target == null && !request.IsDefaultTarget) return;

        var hasVariables = _variableService.HasVariables(request.Variables);
        if (hasVariables)
        {
            foreach (var v in request.Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Value))
                {
                    _dialogService.ShowAlert("请先填写所有变量值。", "变量未填");
                    return;
                }
            }
        }

        var content = _variableService.CompileContent(request.SelectedFile?.Content, request.Variables, request.AdditionalInput);

        if (request.IsDefaultTarget)
        {
            await _webTargetService.SendToDefaultTargetAsync(content, request.Config);
        }
        else
        {
            await _webTargetService.OpenWebTargetAsync(request.Target!, content);
        }
    }
}
