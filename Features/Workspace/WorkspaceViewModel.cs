using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace.LoadWorkspaceData;
using PromptMasterv6.Features.Workspace.FilterFiles;
using PromptMasterv6.Features.Workspace.DeleteFile;
using PromptMasterv6.Features.Workspace.ChangeFileIcon;
using PromptMasterv6.Features.Workspace.GetWorkspaceConfig;
using PromptMasterv6.Features.Workspace.SearchOnGitHub;
using PromptMasterv6.Features.Workspace.SendToWebTarget;
using PromptMasterv6.Features.Shared.Commands;
using PromptMasterv6.Features.Workspace.Editor;
using Markdig;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PromptMasterv6.Features.Workspace;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;
    private readonly DialogService _dialogService;
    private readonly EditorViewModel _editorViewModel;

    public MarkdownPipeline Pipeline { get; }
    public IWorkspaceState State => _state;

    public WorkspaceViewModel(
        IMediator mediator,
        DialogService dialogService,
        IWorkspaceState state,
        EditorViewModel editorViewModel)
    {
        _mediator = mediator;
        _dialogService = dialogService;
        _state = state;
        _editorViewModel = editorViewModel;

        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            _state.SelectedFile = m.File;
            if (m.EnterEditMode) _state.IsEditMode = true;
            WeakReferenceMessenger.Default.Send(new FileSelectedMessage(m.File, m.EnterEditMode));
        });

        WeakReferenceMessenger.Default.Register<JumpToEditPromptMessage>(this, (_, m) =>
        {
            if (m.File != null)
            {
                _state.SelectedFile = m.File;
                _state.IsEditMode = true;
                WeakReferenceMessenger.Default.Send(new FileSelectedMessage(m.File, true));
            }
        });

        WeakReferenceMessenger.Default.Register<RequestPromptFileMessage>(this, (r, m) =>
        {
            if (m.HasReceivedResponse) return;
            var file = _state.Files.FirstOrDefault(f => f.Id == m.PromptId);
            if (file != null)
            {
                m.Reply(new PromptFileResponseMessage { File = file });
            }
        });

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, async (_, _) =>
        {
            await _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder));
            _state.FilesView?.Refresh();

            if (_state.FilesView != null && !_state.FilesView.IsEmpty)
            {
                var firstItem = _state.FilesView.Cast<PromptItem>().FirstOrDefault();
                _state.SelectedFile = firstItem;
                WeakReferenceMessenger.Default.Send(new FileSelectedMessage(firstItem));
            }
            else
            {
                _state.SelectedFile = null;
            }
        });

        WeakReferenceMessenger.Default.Register<ReloadDataMessage>(this, async (_, _) =>
        {
            await LoadDataAsync();
        });
    }

    public async Task InitializeAsync()
    {
        var config = await _mediator.Send(new GetWorkspaceConfigFeature.Query());
        _state.WebDirectTargets = config.WebDirectTargets;
        _state.DefaultWebTargetName = config.DefaultWebTargetName;
    }

    public void SetFiles(ObservableCollection<PromptItem> files)
    {
        foreach (var item in _state.Files)
        {
            item.PropertyChanged -= OnFilePropertyChanged;
        }

        _state.Files.Clear();
        foreach (var file in files)
        {
            _state.Files.Add(file);
        }

        foreach (var item in _state.Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }

        _state.FilesView = CollectionViewSource.GetDefaultView(_state.Files);
        _ = _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder));
    }

    public void SetSelectedFolder(FolderItem? folder)
    {
        _state.SelectedFolder = folder;
        _ = _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder));
        _state.FilesView?.Refresh();
    }

    public async Task LoadDataAsync()
    {
        var result = await _mediator.Send(new LoadWorkspaceDataFeature.Command());

        if (!result.Success || result.Files == null)
        {
            _dialogService.ShowAlert(result.ErrorMessage ?? "加载数据失败", "错误");
            return;
        }

        foreach (var item in _state.Files)
        {
            item.PropertyChanged -= OnFilePropertyChanged;
        }

        _state.Files.Clear();
        foreach (var f in result.Files)
        {
            _state.Files.Add(f);
        }

        foreach (var item in _state.Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }

        await _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder));
        _state.FilesView?.Refresh();

        if (_state.FilesView != null && !_state.FilesView.IsEmpty)
        {
            var firstItem = _state.FilesView.Cast<PromptItem>().FirstOrDefault();
            if (firstItem != null)
            {
                _state.SelectedFile = firstItem;
                WeakReferenceMessenger.Default.Send(new FileSelectedMessage(firstItem));
            }
        }

        _state.IsDirty = false;
    }

    [RelayCommand]
    private void RenameFile(PromptItem? item)
    {
        if (item != null)
        {
            item.IsRenaming = true;
        }
    }

    [RelayCommand]
    private async Task DeleteFile(PromptItem? file)
    {
        if (file == null) return;

        var result = await _mediator.Send(new DeleteFileFeature.Command(file, _state.Files));

        if (result.Success)
        {
            if (result.WasSelected) 
            {
                _state.SelectedFile = null;
                WeakReferenceMessenger.Default.Send(new FileSelectedMessage(null));
            }
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task ChangeFileIcon(PromptItem? file)
    {
        if (file == null) return;

        var result = await _mediator.Send(new ChangeFileIconFeature.Command(file));

        if (result.Success && result.NewIconGeometry != null)
        {
            file.IconGeometry = result.NewIconGeometry;
            RequestSave();
        }
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

        if (!result.Success)
        {
            _dialogService.ShowAlert(result.ErrorMessage ?? "发送失败", "错误");
            return;
        }

        if (result.ShouldClearAdditionalInput)
        {
            _state.AdditionalInput = "";
        }
    }

    [RelayCommand]
    private void RequestSave()
    {
        if (!_state.IsDirty) _state.IsDirty = true;
        WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptItem.LastModified))
            return;

        if (sender is PromptItem changedItem && e.PropertyName == nameof(PromptItem.Content) && sender == _state.SelectedFile)
        {
            WeakReferenceMessenger.Default.Send(new FileContentChangedMessage(changedItem.Content));
        }

        RequestSave();
    }
}
