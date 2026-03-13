using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using PromptMasterv6.Features.Workspace.SelectFilesForImport;
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
    [ObservableProperty] private bool isDirty;

    private bool _enterEditModeOnNextSelection;

    public IDropTarget FileDropHandler { get; }

    public FileListViewModel(IMediator mediator, IWorkspaceState state)
    {
        _mediator = mediator;
        _state = state;
        FileDropHandler = new FileListDropHandler(this);

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            _enterEditModeOnNextSelection = m.EnterEditMode;
            SelectedFile = m.File;
        });

        WeakReferenceMessenger.Default.Register<RequestMoveFileToFolderMessage>(this, async (_, m) => await MoveFileToFolder(m.File, m.TargetFolder));

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
            await CreateFile();
        });

        WeakReferenceMessenger.Default.Register<ImportMarkdownFilesRequestMessage>(this, async (_, m) =>
        {
            if (m.TargetFolder != null)
            {
                SelectedFolder = m.TargetFolder;
            }
            await ImportMarkdownFilesAsync();
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

    [RelayCommand]
    private async Task CreateFile()
    {
        if (SelectedFolder == null) return;

        var result = await _mediator.Send(
            new CreateFileFeature.Command(SelectedFolder.Id),
            default);

        if (result.CreatedFile != null)
        {
            Files.Add(result.CreatedFile);
            WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(result.CreatedFile, EnterEditMode: true));
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task DeleteFile(PromptItem? file)
    {
        if (file == null) return;
        
        var result = await _mediator.Send(
            new DeleteFileFeature.Command(file, Files),
            default);

        if (result.Success)
        {
            if (result.WasSelected) SelectedFile = null;
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task RenameFile(PromptItem? item)
    {
        if (item == null) return;
        
        var result = await _mediator.Send(
            new RenameFileFeature.Command(item),
            default);
    }

    [RelayCommand]
    private async Task ChangeFileIcon(PromptItem? file)
    {
        if (file == null) return;
        
        var result = await _mediator.Send(
            new ChangeFileIconFeature.Command(file),
            default);

        if (result.Success)
        {
            RequestSave();
        }
    }

    public async Task MoveFileToFolder(PromptItem f, FolderItem t)
    {
        if (f == null || t == null) return;
        
        var result = await _mediator.Send(new MoveFileToFolderFeature.Command(f, t));
        
        if (result.Success)
        {
            await LoadFilesForFolder(_currentFolderId);
            if (SelectedFile == f) SelectedFile = null;
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task ImportMarkdownFilesAsync()
    {
        var selectResult = await _mediator.Send(new SelectFilesForImportFeature.Command());
        
        if (!selectResult.Success || selectResult.Files == null || selectResult.Files.Length == 0) 
            return;

        string? targetFolderId = SelectedFolder?.Id;

        if (string.IsNullOrEmpty(targetFolderId))
        {
            return;
        }

        var importResult = await _mediator.Send(new ImportMarkdownFilesFeature.Command(selectResult.Files, targetFolderId));

        if (importResult.Success && importResult.ImportedItems.Count > 0)
        {
            foreach (var item in importResult.ImportedItems)
            {
                Files.Add(item);
            }

            RequestSave();
        }
    }

    public void RequestSave()
    {
        if (!IsDirty) IsDirty = true;
        WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
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

        RequestSave();
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptItem.LastModified))
            return;

        RequestSave();
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
