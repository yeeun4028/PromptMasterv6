using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.AiModels.TestConnection;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace PromptMasterv6.Features.AiModels;

public partial class TestAiConnectionViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    [ObservableProperty] private string? _testStatus;
    [ObservableProperty] private MediaBrush _testStatusColor = MediaBrushes.Gray;

    public TestAiConnectionViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [RelayCommand]
    private async Task ExecuteAsync(AiModelConfig? model)
    {
        if (model == null) return;

        TestStatus = "测试中...";
        TestStatusColor = MediaBrushes.Gray;

        var cmd = new TestAiConnectionFeature.Command(
            model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);

        var result = await _mediator.Send(cmd);

        TestStatus = result.Success && result.ResponseTimeMs.HasValue
            ? $"{result.Message} ({result.ResponseTimeMs}ms)"
            : result.Message;
        TestStatusColor = result.Success ? MediaBrushes.Green : MediaBrushes.Red;
    }
}
