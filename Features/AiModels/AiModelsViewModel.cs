using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.AiModels.TestTranslationBatch;
using System.Threading.Tasks;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace PromptMasterv6.Features.AiModels;

public partial class AiModelsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly IMediator _mediator;

    [ObservableProperty] private AiModelConfig? _selectedSavedModel;
    [ObservableProperty] private string? _translationTestStatus;
    [ObservableProperty] private MediaBrush _translationTestStatusColor = MediaBrushes.Gray;

    public AppConfig Config => _settingsService.Config;

    public AddAiModelViewModel AddModelVM { get; }
    public DeleteAiModelViewModel DeleteModelVM { get; }
    public RenameAiModelViewModel RenameModelVM { get; }
    public TestAiConnectionViewModel TestConnectionVM { get; }

    public AiModelsViewModel(
        SettingsService settingsService,
        IMediator mediator,
        AddAiModelViewModel addModelVM,
        DeleteAiModelViewModel deleteModelVM,
        RenameAiModelViewModel renameModelVM,
        TestAiConnectionViewModel testConnectionVM)
    {
        _settingsService = settingsService;
        _mediator = mediator;
        AddModelVM = addModelVM;
        DeleteModelVM = deleteModelVM;
        RenameModelVM = renameModelVM;
        TestConnectionVM = testConnectionVM;

        AddModelVM.PropertyChanged += (_, _) =>
        {
            if (AddModelVM.AddedModel != null)
            {
                SelectedSavedModel = AddModelVM.AddedModel;
                OnPropertyChanged(nameof(Config));
            }
        };

        DeleteModelVM.PropertyChanged += (_, _) =>
        {
            if (DeleteModelVM.DeletedModel != null && SelectedSavedModel == DeleteModelVM.DeletedModel)
            {
                SelectedSavedModel = null;
            }
        };
    }

    [RelayCommand]
    private async Task TestAiTranslationConnection()
    {
        TranslationTestStatus = "测试中...";
        TranslationTestStatusColor = MediaBrushes.Gray;

        var result = await _mediator.Send(new TestAiTranslationBatchFeature.Command());

        TranslationTestStatus = result.Message;
        TranslationTestStatusColor = result.Success && result.SuccessCount == result.TotalCount
            ? MediaBrushes.Green
            : result.Success && result.SuccessCount > 0
                ? MediaBrushes.Orange
                : MediaBrushes.Red;
    }
}
