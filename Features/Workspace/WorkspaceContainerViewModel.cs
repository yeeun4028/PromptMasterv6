using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Main.Backup.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Workspace.FolderTree;
using PromptMasterv6.Features.Workspace.FileList;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.InitializeAppData;
using PromptMasterv6.Features.Workspace.Messages;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace;

public partial class WorkspaceContainerViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;
    
    public FolderTreeViewModel FolderTreeViewModel { get; }
    public FileListViewModel FileListViewModel { get; }
    
    [ObservableProperty] private bool isDirty;

    public WorkspaceContainerViewModel(
        IMediator mediator,
        IWorkspaceState state,
        FolderTreeViewModel folderTreeViewModel,
        FileListViewModel fileListViewModel)
    {
        _mediator = mediator;
        _state = state;
        FolderTreeViewModel = folderTreeViewModel;
        FileListViewModel = fileListViewModel;

        WeakReferenceMessenger.Default.Register<ApplicationInitializedMessage>(this, async (_, _) =>
        {
            await InitializeAsync();
        });

        WeakReferenceMessenger.Default.Register<BackupCompletedMessage>(this, (_, m) =>
        {
            if (m.Success)
            {
                IsDirty = false;
                _state.IsDirty = false;
            }
        });

        WeakReferenceMessenger.Default.Register<RequestSaveMessage>(this, (_, _) =>
        {
            RequestSave();
        });
    }

    public async Task InitializeAsync()
    {
        var result = await _mediator.Send(new InitializeAppDataFeature.Command());

        if (result.Success)
        {
            _state.Initialize(result.Folders, result.Files);
            
            FolderTreeViewModel.Folders = _state.Folders;
            FileListViewModel.Files = _state.Files;
            
            _state.SelectedFolder = result.SelectedFolder;
            _state.SelectedFile = result.SelectedFile;
            
            FolderTreeViewModel.SelectedFolder = result.SelectedFolder;
            FileListViewModel.SelectedFile = result.SelectedFile;

            IsDirty = false;
            WeakReferenceMessenger.Default.Send(new DataInitializedMessage(_state.Folders, _state.Files));
        }
    }

    public void RequestSave()
    {
        if (!IsDirty) IsDirty = true;
        _state.IsDirty = true;
        WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
    }
}
