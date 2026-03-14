using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Features.Workspace.SyncVariables;
using PromptMasterv6.Core.Messages;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.Variables;

public partial class VariablesViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;

    public VariablesViewModel(IMediator mediator, IWorkspaceState state)
    {
        _mediator = mediator;
        _state = state;
    }

    public async Task SyncVariablesForFileAsync(string? content)
    {
        var varNames = await _mediator.Send(new ParseVariablesQuery(content));
        var result = await _mediator.Send(new SyncVariablesFeature.Command(_state.Variables, varNames));
        _state.HasVariables = result.HasVariables;
    }
}
