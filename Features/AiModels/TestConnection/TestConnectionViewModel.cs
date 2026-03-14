using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.AiModels.Shared;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.AiModels.TestConnection;

public partial class TestConnectionViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    [ObservableProperty] private ConnectionTestStatus _connectionTestStatus = ConnectionTestStatus.Idle;
    [ObservableProperty] private string? _connectionTestMessage;

    public TestConnectionViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [RelayCommand]
    private async Task TestConnection(AiModelConfig? model, CancellationToken cancellationToken)
    {
        if (model == null) return;

        ConnectionTestStatus = ConnectionTestStatus.Testing;
        ConnectionTestMessage = "测试中...";

        var cmd = new TestAiConnectionFeature.Command(
            model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);

        var result = await _mediator.Send(cmd, cancellationToken);

        ConnectionTestMessage = result.DisplayMessage;
        ConnectionTestStatus = result.Success ? ConnectionTestStatus.Success : ConnectionTestStatus.Failed;
    }
}
