using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using MediatR;
using PromptMasterv6.Features.Main.ContentEditor.Messages;
using PromptMasterv6.Features.Main.FileManager.Messages;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public partial class ContentEditorViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;
    private readonly LoggerService _logger;
    private readonly CopyCompiledTextFeature.Handler _copyCompiledTextHandler;
    private readonly SendToWebTargetFeature.Handler _sendToWebTargetHandler;
    private readonly OpenWebTargetFeature.Handler _openWebTargetHandler;
    private readonly SearchOnGitHubFeature.Handler _searchOnGitHubHandler;

    [ObservableProperty] private PromptItem? selectedFile;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private string? previewContent;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";

    private string? _originalContentBeforeEdit;
    private bool _enterEditModeOnNextFileChange;

    public MarkdownPipeline Pipeline { get; }
    public AppConfig Config { get; }

    public ContentEditorViewModel(
        IMediator mediator,
        DialogService dialogService,
        LoggerService logger,
        AppConfig config,
        CopyCompiledTextFeature.Handler copyCompiledTextHandler,
        SendToWebTargetFeature.Handler sendToWebTargetHandler,
        OpenWebTargetFeature.Handler openWebTargetHandler,
        SearchOnGitHubFeature.Handler searchOnGitHubHandler)
    {
        _mediator = mediator;
        _dialogService = dialogService;
        _logger = logger;
        Config = config;
        _copyCompiledTextHandler = copyCompiledTextHandler;
        _sendToWebTargetHandler = sendToWebTargetHandler;
        _openWebTargetHandler = openWebTargetHandler;
        _searchOnGitHubHandler = searchOnGitHubHandler;

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

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        if (_enterEditModeOnNextFileChange)
        {
            IsEditMode = true;
            _enterEditModeOnNextFileChange = false;
        }
        else
        {
            IsEditMode = false;
            WeakReferenceMessenger.Default.Send(new EditModeChangedMessage(false));
        }
        _ = UpdatePreviewContentAsync(value?.Content);
        SafeParseVariables(value?.Content ?? "");
    }

    public async Task SetCurrentFileAsync(PromptItem? file, bool enterEditMode = false)
    {
        _enterEditModeOnNextFileChange = enterEditMode;
        SelectedFile = file;
        if (file != null)
        {
            await UpdatePreviewContentAsync(file.Content);
            SafeParseVariables(file.Content ?? "");
        }
        else
        {
            PreviewContent = null;
            Variables.Clear();
            HasVariables = false;
        }
    }

    private async Task UpdatePreviewContentAsync(string? content)
    {
        PreviewContent = await _mediator.Send(new ConvertHtmlToMarkdownQuery(content));
    }

    private async void SafeParseVariables(string content)
    {
        try
        {
            var varNames = await _mediator.Send(new ParseVariablesQuery(content));
            
            for (int i = Variables.Count - 1; i >= 0; i--)
            {
                if (!varNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
            }

            foreach (var name in varNames)
            {
                if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
            }

            HasVariables = Variables.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "变量解析失败", "SafeParseVariables");
        }
    }

    public async Task OnFileContentChangedAsync(string? content)
    {
        var varNames = await _mediator.Send(new ParseVariablesQuery(content ?? ""));
        
        for (int i = Variables.Count - 1; i >= 0; i--)
        {
            if (!varNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
        }

        foreach (var name in varNames)
        {
            if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
        }

        HasVariables = Variables.Count > 0;
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
            bool contentChanged = !string.Equals(_originalContentBeforeEdit, SelectedFile.Content, StringComparison.Ordinal);

            if (contentChanged)
            {
                SelectedFile.LastModified = DateTime.Now;
                WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
            }

            IsEditMode = false;
            PreviewContent = await _mediator.Send(new ConvertHtmlToMarkdownQuery(SelectedFile.Content));
            _originalContentBeforeEdit = null;
            
            WeakReferenceMessenger.Default.Send(new EditModeChangedMessage(false));
            return;
        }

        _originalContentBeforeEdit = SelectedFile.Content;
        IsEditMode = true;
        WeakReferenceMessenger.Default.Send(new EditModeChangedMessage(true));
    }

    [RelayCommand]
    private async Task CopyCompiledText()
    {
        await _copyCompiledTextHandler.Handle(
            new CopyCompiledTextFeature.Command(SelectedFile, Variables, AdditionalInput),
            default);
    }

    [RelayCommand]
    private async Task SendDefaultWebTarget()
    {
        if (SelectedFile == null) return;

        if (HasVariables)
        {
            foreach (var v in Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Value))
                {
                    _dialogService.ShowAlert("请先填写所有变量值。", "变量未填");
                    return;
                }
            }
        }

        await _sendToWebTargetHandler.Handle(
            new SendToWebTargetFeature.Command(SelectedFile, Variables, AdditionalInput, Config.WebDirectTargets, Config.DefaultWebTargetName),
            default);
        AdditionalInput = "";
    }

    [RelayCommand]
    private async Task OpenWebTarget(WebTarget? target)
    {
        if (target == null || SelectedFile == null) return;

        if (HasVariables)
        {
            foreach (var v in Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Value))
                {
                    _dialogService.ShowAlert("请先填写所有变量值。", "变量未填");
                    return;
                }
            }
        }

        await _openWebTargetHandler.Handle(
            new OpenWebTargetFeature.Command(SelectedFile, Variables, AdditionalInput, target),
            default);
        AdditionalInput = "";
    }

    [RelayCommand]
    private async Task SearchOnGitHub()
    {
        var query = AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return;
        }

        var result = await _searchOnGitHubHandler.Handle(new SearchOnGitHubFeature.Command(query), default);
        
        if (result.Success)
        {
            AdditionalInput = "";
        }
        else
        {
            _dialogService.ShowAlert($"打开 GitHub 失败: {result.ErrorMessage}", "错误");
        }
    }
}
