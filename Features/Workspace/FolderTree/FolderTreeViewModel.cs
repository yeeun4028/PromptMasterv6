using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
    [ObservableProperty] private bool isDirty;

    public IDropTarget FolderDropHandler { get; }

    public FolderTreeViewModel(IMediator mediator, IWorkspaceState state)
    {
        _mediator = mediator;
        _state = state;
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
            await ChangeFolderIcon(m.Folder);
        });

        WeakReferenceMessenger.Default.Register<RenameFolderRequestMessage>(this, async (_, m) =>
        {
            await RenameFolder(m.Folder);
        });

        WeakReferenceMessenger.Default.Register<DeleteFolderRequestMessage>(this, async (_, m) =>
        {
            await DeleteFolder(m.Folder);
        });
    }

    partial void OnSelectedFolderChanged(FolderItem? value)
    {
        _state.SelectedFolder = value;
        
        if (value == null) return;

        _mediator.Publish(new FolderSelectedEvent(value.Id));
        WeakReferenceMessenger.Default.Send(new FolderSelectionChangedMessage(value));
    }

    [RelayCommand]
    private async Task CreateFolder()
    {
        var result = await _mediator.Send(
            new CreateFolderFeature.Command(Folders), 
            default);
        
        if (result.CreatedFolder != null)
        {
            SelectedFolder = result.CreatedFolder;
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task DeleteFolder(FolderItem? folder)
    {
        if (folder == null) return;

        var result = await _mediator.Send(
            new DeleteFolderFeature.Command(folder, Folders, null),
            default);

        if (result.Success)
        {
            if (result.WasSelected) SelectedFolder = null;
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task ChangeFolderIcon(FolderItem? f)
    {
        if (f == null) return;
        
        var result = await _mediator.Send(
            new ChangeFolderIconFeature.Command(f),
            default);

        if (result.Success)
        {
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task RenameFolder(FolderItem? f)
    {
        if (f == null) return;
        
        var result = await _mediator.Send(
            new RenameFolderFeature.Command(f),
            default);

        if (result.Success)
        {
            RequestSave();
        }
    }

    public async Task ReorderFolders(int oldIndex, int newIndex)
    {
        var result = await _mediator.Send(new ReorderFolderFeature.Command(Folders, oldIndex, newIndex));
        if (result.Success)
        {
            RequestSave();
        }
    }

    public void RequestSave()
    {
        if (!IsDirty) IsDirty = true;
        WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
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
                await _vm.ReorderFolders(oldIndex, newIndex);
            }
        }
    }
}
