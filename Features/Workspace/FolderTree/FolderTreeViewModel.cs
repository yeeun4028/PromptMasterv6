using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Workspace.CreateFolder;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.Workspace.RenameFolder;
using PromptMasterv6.Features.Workspace.DeleteFolder;
using PromptMasterv6.Features.Workspace.ChangeFolderIcon;
using PromptMasterv6.Features.Workspace.ReorderFolder;
using PromptMasterv6.Features.Workspace.State;
using System.Collections.ObjectModel;
using GongSolutions.Wpf.DragDrop;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using DragDropEffects = System.Windows.DragDropEffects;

namespace PromptMasterv6.Features.Workspace.FolderTree;

public partial class FolderTreeViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;

    public IDropTarget FolderDropHandler { get; }

    public CreateFolderViewModel CreateFolderVM { get; }
    public RenameFolderViewModel RenameFolderVM { get; }
    public DeleteFolderViewModel DeleteFolderVM { get; }
    public ChangeFolderIconViewModel ChangeFolderIconVM { get; }
    public ReorderFolderViewModel ReorderFolderVM { get; }

    public FolderTreeViewModel(
        IMediator mediator, 
        IWorkspaceState state,
        CreateFolderViewModel createFolderVM,
        RenameFolderViewModel renameFolderVM,
        DeleteFolderViewModel deleteFolderVM,
        ChangeFolderIconViewModel changeFolderIconVM,
        ReorderFolderViewModel reorderFolderVM)
    {
        _mediator = mediator;
        _state = state;
        CreateFolderVM = createFolderVM;
        RenameFolderVM = renameFolderVM;
        DeleteFolderVM = deleteFolderVM;
        ChangeFolderIconVM = changeFolderIconVM;
        ReorderFolderVM = reorderFolderVM;
        Folders = _state.Folders;
        FolderDropHandler = new FolderTreeDropHandler(this);

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, (_, m) =>
        {
            if (m.Folder != null && Folders.Contains(m.Folder))
            {
                SelectedFolder = m.Folder;
            }
        });

        WeakReferenceMessenger.Default.Register<ChangeFolderIconRequestMessage>(this, async (_, m) =>
        {
            await ChangeFolderIconVM.ExecuteCommand.ExecuteAsync(m.Folder);
        });

        WeakReferenceMessenger.Default.Register<RenameFolderRequestMessage>(this, async (_, m) =>
        {
            await RenameFolderVM.ExecuteCommand.ExecuteAsync(m.Folder);
        });

        WeakReferenceMessenger.Default.Register<DeleteFolderRequestMessage>(this, async (_, m) =>
        {
            await DeleteFolderVM.ExecuteCommand.ExecuteAsync(m.Folder);
        });
    }

    partial void OnSelectedFolderChanged(FolderItem? value)
    {
        _state.SelectedFolder = value;
        
        if (value == null) return;

        _mediator.Publish(new FolderSelectedEvent(value.Id));
        WeakReferenceMessenger.Default.Send(new FolderSelectionChangedMessage(value));
    }

    private sealed class FolderTreeDropHandler : IDropTarget
    {
        private readonly FolderTreeViewModel _vm;
        
        public FolderTreeDropHandler(FolderTreeViewModel vm) => _vm = vm;

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is FolderItem && dropInfo.TargetItem is FolderItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public async void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is FolderItem sourceFolder && dropInfo.TargetItem is FolderItem)
            {
                int oldIndex = _vm.Folders.IndexOf(sourceFolder);
                int newIndex = dropInfo.InsertIndex;
                if (oldIndex < newIndex) newIndex--;
                if (oldIndex == newIndex) return;
                await _vm.ReorderFolderVM.ReorderAsync(oldIndex, newIndex);
            }
        }
    }
}
