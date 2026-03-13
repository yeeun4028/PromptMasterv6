using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using MediatR;
using PromptMasterv6.Features.Main.ContentEditor.Messages;
using PromptMasterv6.Features.Main.FileManager.Messages;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public partial class ContentEditorViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    [ObservableProperty] private PromptItem? selectedFile;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private string? previewContent;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";

    private string? _originalContentBeforeEdit;

    public MarkdownPipeline Pipeline { get; }
    public AppConfig Config { get; }

    public ContentEditorViewModel(
        IMediator mediator,
        AppConfig config)
    {
        _mediator = mediator;
        Config = config;

        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, async (_, m) =>
        {
            await SetCurrentFileAsync(m.File, m.EnterEditMode);
        });

        WeakReferenceMessenger.Default.Register<FileSelectedMessage>(this, async (_, m) =>
        {
            await SetCurrentFileAsync(m.File, m.EnterEditMode);
        });

        WeakReferenceMessenger.Default.Register<ToggleEditModeRequestMessage>(this, async (_, _) =>
        {
            await ToggleEditMode();
        });
    }

    private async Task SetCurrentFileAsync(PromptItem? file, bool enterEditMode = false)
    {
        var result = await _mediator.Send(new SetCurrentFileFeature.Command(file, enterEditMode, Variables));
        
        SelectedFile = result.SelectedFile;
        IsEditMode = result.IsEditMode;
        PreviewContent = result.PreviewContent;
        HasVariables = result.HasVariables;

        WeakReferenceMessenger.Default.Send(new EditModeChangedMessage(result.IsEditMode));
    }

    [RelayCommand]
    private async Task ToggleEditMode()
    {
        if (SelectedFile == null)
        {
            IsEditMode = false;
            return;
        }

        if (IsEditMode)
        {
            var result = await _mediator.Send(new ToggleEditModeFeature.Command(
                SelectedFile, IsEditMode, _originalContentBeforeEdit));

            IsEditMode = result.NewEditMode;
            PreviewContent = result.PreviewContent;
            _originalContentBeforeEdit = null;

            WeakReferenceMessenger.Default.Send(new EditModeChangedMessage(result.NewEditMode));
            return;
        }

        _originalContentBeforeEdit = SelectedFile.Content;
        IsEditMode = true;
        WeakReferenceMessenger.Default.Send(new EditModeChangedMessage(true));
    }

    [RelayCommand]
    private async Task CopyCompiledText()
    {
        await _mediator.Send(new CopyCompiledTextFeature.Command(SelectedFile, Variables, AdditionalInput));
    }

    [RelayCommand]
    private async Task SendDefaultWebTarget()
    {
        if (SelectedFile == null) return;

        await _mediator.Send(new SendToWebTargetFeature.Command(
            SelectedFile, Variables, AdditionalInput, Config.WebDirectTargets, Config.DefaultWebTargetName));
        
        AdditionalInput = "";
    }

    [RelayCommand]
    private async Task OpenWebTarget(WebTarget? target)
    {
        if (target == null || SelectedFile == null) return;

        await _mediator.Send(new OpenWebTargetFeature.Command(
            SelectedFile, Variables, AdditionalInput, target));
        
        AdditionalInput = "";
    }

    [RelayCommand]
    private async Task SearchOnGitHub()
    {
        var query = AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query)) return;

        await _mediator.Send(new SearchOnGitHubFeature.Command(query));
        AdditionalInput = "";
    }
}
