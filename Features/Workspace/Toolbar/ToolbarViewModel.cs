using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.SearchOnGitHub;
using PromptMasterv6.Features.Workspace.SendToWebTarget;
using PromptMasterv6.Features.Shared.Commands;
using PromptMasterv6.Features.Workspace.GetWorkspaceConfig;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.Toolbar;

public partial class ToolbarViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;
    private readonly DialogService _dialogService;

    [ObservableProperty]
    private List<WebTarget> _webDirectTargets = new();

    [ObservableProperty]
    private string? _defaultWebTargetName;

    public ToolbarViewModel(
        IMediator mediator, 
        IWorkspaceState state,
        DialogService dialogService)
    {
        _mediator = mediator;
        _state = state;
        _dialogService = dialogService;
    }

    public async Task InitializeAsync()
    {
        var config = await _mediator.Send(new GetWorkspaceConfigFeature.Query());
        WebDirectTargets = config.WebDirectTargets;
        DefaultWebTargetName = config.DefaultWebTargetName;
    }

    [RelayCommand]
    private async Task CopyCompiledText()
    {
        var variablesDict = _state.Variables.ToDictionary(v => v.Name, v => v.Value ?? "");
        await _mediator.Send(new CopyCompiledTextCommand(
            _state.SelectedFile?.Content, 
            variablesDict, 
            _state.AdditionalInput));
    }

    [RelayCommand]
    private async Task SendDefaultWebTarget()
    {
        var result = await _mediator.Send(new SendToWebTargetFeature.Command(
            _state.SelectedFile,
            _state.Variables,
            _state.HasVariables,
            _state.AdditionalInput,
            AllTargets: WebDirectTargets,
            DefaultTargetName: DefaultWebTargetName));

        await HandleSendResultAsync(result);
    }

    [RelayCommand]
    private async Task OpenWebTarget(WebTarget? target)
    {
        if (target == null) return;

        var result = await _mediator.Send(new SendToWebTargetFeature.Command(
            _state.SelectedFile,
            _state.Variables,
            _state.HasVariables,
            _state.AdditionalInput,
            Target: target));

        await HandleSendResultAsync(result);
    }

    [RelayCommand]
    private async Task SearchOnGitHub()
    {
        var query = _state.AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return;
        }

        var result = await _mediator.Send(new SearchOnGitHubFeature.Command(query));

        if (result.Success)
        {
            _state.AdditionalInput = "";
        }
        else
        {
            _dialogService.ShowAlert(result.ErrorMessage ?? "搜索失败", "错误");
        }
    }

    private Task HandleSendResultAsync(SendToWebTargetFeature.Result result)
    {
        if (!result.Success)
        {
            _dialogService.ShowAlert(result.ErrorMessage ?? "发送失败", "错误");
            return Task.CompletedTask;
        }

        if (result.ShouldClearAdditionalInput)
        {
            _state.AdditionalInput = "";
        }

        return Task.CompletedTask;
    }
}
