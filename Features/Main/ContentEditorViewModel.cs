using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markdig;
using MediatR;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Features.Shared.Commands;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main;

public partial class ContentEditorViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;
    private readonly LoggerService _logger;

    [ObservableProperty] private PromptItem? currentFile;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private string? previewContent;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";

    private string? _originalContentBeforeEdit;

    public MarkdownPipeline Pipeline { get; }
    public AppConfig Config { get; }

    public event Action? ContentChanged;

    public ContentEditorViewModel(
        IMediator mediator,
        DialogService dialogService,
        LoggerService logger,
        AppConfig config)
    {
        _mediator = mediator;
        _dialogService = dialogService;
        _logger = logger;
        Config = config;

        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();
    }

    partial void OnCurrentFileChanged(PromptItem? value)
    {
        IsEditMode = false;
        _ = UpdatePreviewContentAsync(value?.Content);
        SafeParseVariables(value?.Content ?? "");
    }

    public async Task SetCurrentFileAsync(PromptItem? file)
    {
        CurrentFile = file;
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
        if (CurrentFile == null)
        {
            IsEditMode = false;
            return;
        }

        if (IsEditMode)
        {
            bool contentChanged = !string.Equals(_originalContentBeforeEdit, CurrentFile.Content, StringComparison.Ordinal);

            if (contentChanged)
            {
                CurrentFile.LastModified = DateTime.Now;
                ContentChanged?.Invoke();
            }

            IsEditMode = false;
            PreviewContent = await _mediator.Send(new ConvertHtmlToMarkdownQuery(CurrentFile.Content));
            _originalContentBeforeEdit = null;
            return;
        }

        _originalContentBeforeEdit = CurrentFile.Content;
        IsEditMode = true;
    }

    [RelayCommand]
    private async Task CopyCompiledText()
    {
        var variablesDict = Variables.ToDictionary(v => v.Name, v => v.Value ?? "");
        await _mediator.Send(new CopyCompiledTextCommand(CurrentFile?.Content, variablesDict, AdditionalInput));
    }

    [RelayCommand]
    private async Task SendDefaultWebTarget()
    {
        if (CurrentFile == null) return;

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

        var variablesDict = Variables.ToDictionary(v => v.Name, v => v.Value ?? "");
        var content = await _mediator.Send(new CompileContentQuery(CurrentFile?.Content, variablesDict, AdditionalInput));
        await _mediator.Send(new SendToDefaultTargetCommand(content, Config.WebDirectTargets, Config.DefaultWebTargetName));
        AdditionalInput = "";
    }

    [RelayCommand]
    private async Task OpenWebTarget(WebTarget? target)
    {
        if (target == null || CurrentFile == null) return;

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

        var variablesDict = Variables.ToDictionary(v => v.Name, v => v.Value ?? "");
        var content = await _mediator.Send(new CompileContentQuery(CurrentFile?.Content, variablesDict, AdditionalInput));
        await _mediator.Send(new ExecuteWebTargetCommand(target, content));
        AdditionalInput = "";
    }

    [RelayCommand]
    private void SearchOnGitHub()
    {
        var query = AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return;
        }

        try
        {
            var url = $"https://github.com/search?q={Uri.EscapeDataString(query)}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            AdditionalInput = "";
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "SearchOnGitHub Failed", "ContentEditorViewModel");
            _dialogService.ShowAlert($"打开 GitHub 失败: {ex.Message}", "错误");
        }
    }
}
