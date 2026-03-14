using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Features.AiModels.Shared;
using PromptMasterv6.Features.AiModels.TestConnection;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.AiModels.EditSelectedModel;

public partial class EditSelectedModelViewModel : ObservableObject, IDisposable
{
    private readonly AiModelSelectionState _selectionState;
    private readonly SettingsService _settingsService;
    private bool _disposed;

    public AiModelSelectionState SelectionState => _selectionState;
    public AppConfig Config => _settingsService.Config;
    public TestConnectionViewModel TestConnectionViewModel { get; }

    public EditSelectedModelViewModel(
        SettingsService settingsService, 
        AiModelSelectionState selectionState,
        TestConnectionViewModel testConnectionViewModel)
    {
        _settingsService = settingsService;
        _selectionState = selectionState;
        TestConnectionViewModel = testConnectionViewModel;
        
        _selectionState.ModelSelected += OnModelSelected;
    }

    private void OnModelSelected(PromptMasterv6.Features.Shared.Models.AiModelConfig? model)
    {
        OnPropertyChanged(nameof(SelectionState));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _selectionState.ModelSelected -= OnModelSelected;
            _disposed = true;
        }
    }
}
