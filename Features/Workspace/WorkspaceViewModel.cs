using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using Markdig;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.Workspace.LoadWorkspaceData;
using PromptMasterv6.Features.Workspace.GetWorkspaceConfig;
using PromptMasterv6.Features.Workspace.HandleFolderSelection;
using PromptMasterv6.Features.Workspace.HandleFileSelection;
using PromptMasterv6.Features.Workspace.HandleJumpToEdit;
using PromptMasterv6.Features.Workspace.HandlePromptFileRequest;
using PromptMasterv6.Features.Workspace.InitializeWorkspace;
using PromptMasterv6.Features.Workspace.HandleFilePropertyChanged;
using PromptMasterv6.Core.Messages;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;

    public MarkdownPipeline Pipeline { get; }
    public IWorkspaceState State => _state;

    public WorkspaceViewModel(IMediator mediator, IWorkspaceState state)
    {
        _mediator = mediator;
        _state = state;

        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, async (_, m) =>
        {
            await _mediator.Send(new HandleFileSelectionFeature.Command(m.File, m.EnterEditMode), CancellationToken.None);
        });

        WeakReferenceMessenger.Default.Register<JumpToEditPromptMessage>(this, async (_, m) =>
        {
            await _mediator.Send(new HandleJumpToEditFeature.Command(m.File), CancellationToken.None);
        });

        WeakReferenceMessenger.Default.Register<RequestPromptFileMessage>(this, async (_, m) =>
        {
            await _mediator.Send(new HandlePromptFileRequestFeature.Command(m.PromptId, m), CancellationToken.None);
        });

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, async (_, _) =>
        {
            await _mediator.Send(new HandleFolderSelectionFeature.Command(), CancellationToken.None);
        });

        WeakReferenceMessenger.Default.Register<ReloadDataMessage>(this, async (_, _) =>
        {
            await LoadDataAsync();
        });
    }

    [RelayCommand]
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new InitializeWorkspaceFeature.Command(), cancellationToken);
    }

    [RelayCommand]
    public async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new LoadWorkspaceDataFeature.Command(), cancellationToken);
    }

    [RelayCommand]
    private void RequestSave()
    {
        if (!_state.IsDirty) _state.IsDirty = true;
        WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
    }

    private async void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var result = await _mediator.Send(new HandleFilePropertyChangedFeature.Command(sender, e), CancellationToken.None);
        if (result.ShouldSave)
        {
            RequestSave();
        }
    }
}
