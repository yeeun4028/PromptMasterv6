using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Workspace.FolderTree;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.CreateFile;
using PromptMasterv6.Features.Workspace.DeleteFile;
using PromptMasterv6.Features.Workspace.RenameFile;
using PromptMasterv6.Features.Workspace.ChangeFileIcon;
using PromptMasterv6.Features.Workspace.MoveFile;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.Workspace.ImportFiles;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using GongSolutions.Wpf.DragDrop;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using DragDropEffects = System.Windows.DragDropEffects;

namespace PromptMasterv6.Features.Workspace.FileList;

public partial class FileListViewModel : ObservableObject, INotificationHandler<FolderSelectedEvent>
{
    private readonly IMediator _mediator;
    private readonly IWorkspaceState _state;
    private string? _currentFolderId;

    [ObservableProperty] private ObservableCollection<PromptItem> files = new();
    [ObservableProperty] private PromptItem? selectedFile;
    [ObservableProperty] private FolderItem? selectedFolder;

    private bool _enterEditModeOnNextSelection;

    public IDropTarget FileDropHandler { get; }

    public CreateFileViewModel CreateFileVM { get; }
    public RenameFileViewModel RenameFileVM { get; }
    public DeleteFileViewModel DeleteFileVM { get; }
    public ChangeFileIconViewModel ChangeFileIconVM { get; }
    public MoveFileViewModel MoveFileVM { get; }
    public ImportMarkdownFilesViewModel ImportMarkdownFilesVM { get; }

    public FileListViewModel(
        IMediator mediator, 
        IWorkspaceState state,
        CreateFileViewModel createFileVM,
        RenameFileViewModel renameFileVM,
        DeleteFileViewModel deleteFileVM,
        ChangeFileIconViewModel changeFileIconVM,
        MoveFileViewModel moveFileVM,
        ImportMarkdownFilesViewModel importMarkdownFilesVM)
    {
        _mediator = mediator;
        _state = state;
        CreateFileVM = createFileVM;
        RenameFileVM = renameFileVM;
        DeleteFileVM = deleteFileVM;
        ChangeFileIconVM = changeFileIconVM;
        MoveFileVM = moveFileVM;
        ImportMarkdownFilesVM = importMarkdownFilesVM;
        FileDropHandler = new FileListDropHandler(this);

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            _enterEditModeOnNextSelection = m.EnterEditMode;
            SelectedFile = m.File;
        });

        WeakReferenceMessenger.Default.Register<RequestMoveFileToFolderMessage>(this, async (_, m) => 
        {
            await MoveFileVM.ExecuteCommand.ExecuteAsync(new MoveFileParameter(m.File, m.TargetFolder));
            await LoadFilesForFolder(_currentFolderId);
        });

        WeakReferenceMessenger.Default.Register<RequestPromptFileMessage>(this, (_, m) =>
        {
            if (m.HasReceivedResponse) return;
            var file = Files.FirstOrDefault(f => f.Id == m.PromptId);
            if (file != null)
            {
                m.Reply(new PromptFileResponseMessage { File = file });
            }
        });

        WeakReferenceMessenger.Default.Register<CreateFileRequestMessage>(this, async (_, m) =>
        {
            if (m.TargetFolder != null)
            {
                SelectedFolder = m.TargetFolder;
            }
            await CreateFileVM.ExecuteCommand.ExecuteAsync(null);
        });

        WeakReferenceMessenger.Default.Register<ImportMarkdownFilesRequestMessage>(this, async (_, m) =>
        {
            if (m.TargetFolder != null)
            {
                _state.SelectedFolder = m.TargetFolder;
            }
            await ImportMarkdownFilesVM.ExecuteCommand.ExecuteAsync(null);
            await LoadFilesForFolder(_currentFolderId);
        });
    }

    public async Task Handle(FolderSelectedEvent notification, CancellationToken cancellationToken)
    {
        _currentFolderId = notification.FolderId;
        await LoadFilesForFolder(notification.FolderId);
    }

    private async Task LoadFilesForFolder(string? folderId)
    {
        var result = await _mediator.Send(new GetFilesByFolderQuery(folderId));
        Files = new ObservableCollection<PromptItem>(result);
        
        _state.Files.Clear();
        foreach (var file in Files)
        {
            _state.Files.Add(file);
        }
        
        Files.CollectionChanged += OnFilesCollectionChanged;
        foreach (var item in Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }

        if (Files.Count > 0)
        {
            SelectedFile = Files[0];
        }
        else
        {
            SelectedFile = null;
        }
    }

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        _state.SelectedFile = value;
        
        var enterEditMode = _enterEditModeOnNextSelection;
        _enterEditModeOnNextSelection = false;
        WeakReferenceMessenger.Default.Send(new FileSelectedMessage(value, enterEditMode));
    }

    private void OnFilesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PromptItem item in e.NewItems)
            {
                item.PropertyChanged += OnFilePropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (PromptItem item in e.OldItems)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptItem.LastModified))
            return;
    }

    public void Cleanup()
    {
        if (Files != null)
        {
            Files.CollectionChanged -= OnFilesCollectionChanged;
            foreach (var item in Files)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private sealed class FileListDropHandler : IDropTarget
    {
        private readonly FileListViewModel _vm;
        
        public FileListDropHandler(FileListViewModel vm) => _vm = vm;

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is PromptItem && dropInfo.TargetItem is FolderItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is PromptItem file && dropInfo.TargetItem is FolderItem fileTarget)
            {
                WeakReferenceMessenger.Default.Send(new RequestMoveFileToFolderMessage(file, fileTarget));
            }
        }
    }
}
