using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using MediatR;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Workspace.ToggleEditMode;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Core.Messages;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.Editor;

public partial class EditorViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;
    private string? _originalContentBeforeEdit;

    public MarkdownPipeline Pipeline { get; }

    public EditorViewModel(IMediator mediator, IWorkspaceState state)
    {
        _mediator = mediator;
        _state = state;

        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        WeakReferenceMessenger.Default.Register<FileSelectedMessage>(this, async (_, m) =>
        {
            await OnFileSelectedAsync(m.File);
            if (m.EnterEditMode)
            {
                _state.IsEditMode = true;
            }
        });
    }

    private async Task OnFileSelectedAsync(PromptItem? file)
    {
        _state.IsEditMode = false;
        
        if (file == null)
        {
            _state.PreviewContent = null;
            return;
        }

        var previewContent = await _mediator.Send(new ConvertHtmlToMarkdownQuery(file.Content));
        _state.PreviewContent = previewContent;
    }

    [RelayCommand]
    private async Task ToggleEditMode()
    {
        var result = await _mediator.Send(new ToggleEditModeFeature.Command(
            _state.SelectedFile,
            _state.IsEditMode,
            _originalContentBeforeEdit));

        if (result.NewLastModified.HasValue && _state.SelectedFile != null)
        {
            _state.SelectedFile.LastModified = result.NewLastModified.Value;
        }

        if (result.ShouldSave)
        {
            WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
        }

        _state.IsEditMode = result.NewEditMode;
        _originalContentBeforeEdit = result.OriginalContentBeforeEdit;

        if (!_state.IsEditMode && _state.SelectedFile != null)
        {
            _state.PreviewContent = await _mediator.Send(new ConvertHtmlToMarkdownQuery(_state.SelectedFile.Content));
        }
    }
}
