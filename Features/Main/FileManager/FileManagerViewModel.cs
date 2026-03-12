using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Shared.Dialogs;
using PromptMasterv6.Features.Main.Backup.Messages;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Main.FileManager.Messages;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using GongSolutions.Wpf.DragDrop;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using DragDropEffects = System.Windows.DragDropEffects;

namespace PromptMasterv6.Features.Main.FileManager;

public partial class FileManagerViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDataService _localDataService;
    private readonly DialogService _dialogService;
    private readonly LoggerService _logger;
    private readonly IMediator _mediator;
    private readonly CreateFolderFeature.Handler _createFolderHandler;
    private readonly DeleteFolderFeature.Handler _deleteFolderHandler;
    private readonly RenameFolderFeature.Handler _renameFolderHandler;
    private readonly ChangeFolderIconFeature.Handler _changeFolderIconHandler;
    private readonly CreateFileFeature.Handler _createFileHandler;
    private readonly DeleteFileFeature.Handler _deleteFileHandler;
    private readonly RenameFileFeature.Handler _renameFileHandler;
    private readonly ChangeFileIconFeature.Handler _changeFileIconHandler;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;
    [ObservableProperty] private ObservableCollection<PromptItem> files = new();
    [ObservableProperty] private PromptItem? selectedFile;
    [ObservableProperty] private ICollectionView? filesView;
    [ObservableProperty] private bool isDirty;

    public IDropTarget FolderDropHandler { get; }

    private bool _enterEditModeOnNextSelection;

    public FileManagerViewModel(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService,
        DialogService dialogService,
        LoggerService logger,
        IMediator mediator,
        CreateFolderFeature.Handler createFolderHandler,
        DeleteFolderFeature.Handler deleteFolderHandler,
        RenameFolderFeature.Handler renameFolderHandler,
        ChangeFolderIconFeature.Handler changeFolderIconHandler,
        CreateFileFeature.Handler createFileHandler,
        DeleteFileFeature.Handler deleteFileHandler,
        RenameFileFeature.Handler renameFileHandler,
        ChangeFileIconFeature.Handler changeFileIconHandler)
    {
        _dataService = dataService;
        _localDataService = localDataService;
        _dialogService = dialogService;
        _logger = logger;
        _mediator = mediator;
        _createFolderHandler = createFolderHandler;
        _deleteFolderHandler = deleteFolderHandler;
        _renameFolderHandler = renameFolderHandler;
        _changeFolderIconHandler = changeFolderIconHandler;
        _createFileHandler = createFileHandler;
        _deleteFileHandler = deleteFileHandler;
        _renameFileHandler = renameFileHandler;
        _changeFileIconHandler = changeFileIconHandler;
        
        FolderDropHandler = new FileManagerFolderDropHandler(this);

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            _enterEditModeOnNextSelection = m.EnterEditMode;
            SelectedFile = m.File;
        });

        WeakReferenceMessenger.Default.Register<RequestMoveFileToFolderMessage>(this, (_, m) => MoveFileToFolder(m.File, m.TargetFolder));

        WeakReferenceMessenger.Default.Register<RequestPromptFileMessage>(this, (_, m) =>
        {
            if (m.HasReceivedResponse) return;
            var file = Files.FirstOrDefault(f => f.Id == m.PromptId);
            if (file != null)
            {
                m.Reply(new PromptFileResponseMessage { File = file });
            }
        });

        WeakReferenceMessenger.Default.Register<BackupCompletedMessage>(this, (_, m) =>
        {
            if (m.Success)
            {
                IsDirty = false;
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
        UpdateFilesViewFilter();
        FilesView?.Refresh();
            
        if (FilesView != null && !FilesView.IsEmpty)
        {
            var firstItem = FilesView.Cast<PromptItem>().FirstOrDefault();
            SelectedFile = firstItem;
        }
        else
        {
            SelectedFile = null;
        }
        
        WeakReferenceMessenger.Default.Send(new FolderSelectionChangedMessage(value));
    }

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        var enterEditMode = _enterEditModeOnNextSelection;
        _enterEditModeOnNextSelection = false;
        WeakReferenceMessenger.Default.Send(new FileSelectedMessage(value, enterEditMode));
    }

    public async Task InitializeAsync()
    {
        AppData data;
        try
        {
            data = await _dataService.LoadAsync();
        }
        catch
        {
            data = new AppData();
        }

        if ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0)
        {
            try
            {
                data = await _localDataService.LoadAsync();
            }
            catch
            {
                data = new AppData();
            }
        }

        Files = new ObservableCollection<PromptItem>(data.Files ?? new());
        Files.CollectionChanged += OnFilesCollectionChanged;
        foreach (var item in Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }

        Folders = new ObservableCollection<FolderItem>(data.Folders ?? new());
        if (Folders.Count == 0)
        {
            var defaultFolder = new FolderItem { Name = "默认" };
            Folders.Add(defaultFolder);
            SelectedFolder = defaultFolder;
        }
        else
        {
            SelectedFolder = Folders.FirstOrDefault();
        }

        if (SelectedFolder != null)
        {
            foreach (var f in Files)
            {
                if (string.IsNullOrWhiteSpace(f.FolderId))
                {
                    f.FolderId = SelectedFolder.Id;
                }
            }
        }

        FilesView = CollectionViewSource.GetDefaultView(Files);
        UpdateFilesViewFilter();
        FilesView?.Refresh();

        if (FilesView != null && !FilesView.IsEmpty)
        {
            var firstItem = FilesView.Cast<PromptItem>().FirstOrDefault();
            if (firstItem != null)
            {
                SelectedFile = firstItem;
            }
        }

        IsDirty = false;
        WeakReferenceMessenger.Default.Send(new DataInitializedMessage(Folders, Files));
    }

    public void UpdateFilesViewFilter()
    {
        if (FilesView == null) return;

        var selectedFolderId = SelectedFolder?.Id;
        FilesView.Filter = item =>
        {
            if (item is not PromptItem f) return false;
            if (string.IsNullOrWhiteSpace(selectedFolderId)) return true;
            return string.Equals(f.FolderId, selectedFolderId, StringComparison.Ordinal);
        };
    }

    [RelayCommand]
    private async Task CreateFolder()
    {
        var result = await _createFolderHandler.Handle(
            new CreateFolderFeature.Command(Folders), 
            default);
        
        if (result.CreatedFolder != null)
        {
            SelectedFolder = result.CreatedFolder;
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task CreateFile()
    {
        if (SelectedFolder == null) return;

        var result = await _createFileHandler.Handle(
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
    private async Task DeleteFolder(FolderItem? folder)
    {
        if (folder == null) return;

        var result = await _deleteFolderHandler.Handle(
            new DeleteFolderFeature.Command(folder, Folders, Files),
            default);

        if (result.Success)
        {
            if (result.WasSelected) SelectedFolder = null;
            WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(null, EnterEditMode: false));
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task ChangeFolderIcon(FolderItem? f)
    {
        if (f == null) return;
        
        var result = await _changeFolderIconHandler.Handle(
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
        
        var result = await _renameFolderHandler.Handle(
            new RenameFolderFeature.Command(f),
            default);

        if (result.Success)
        {
            RequestSave();
        }
    }

    public void ReorderFolders(int oldIndex, int newIndex)
    {
        Folders.Move(oldIndex, newIndex);
        RequestSave();
    }

    [RelayCommand]
    private async Task RenameFile(PromptItem? item)
    {
        if (item == null) return;
        
        var result = await _renameFileHandler.Handle(
            new RenameFileFeature.Command(item),
            default);
    }

    [RelayCommand]
    private async Task ImportMarkdownFilesAsync()
    {
        string filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*";
        var files = _dialogService.ShowOpenFilesDialog(filter);

        if (files == null || files.Length == 0) return;

        var targetFolder = SelectedFolder;
        if (targetFolder == null)
        {
            targetFolder = new FolderItem 
            { 
                Id = Guid.NewGuid().ToString("N"),
                Name = "导入" 
            };
            Folders.Add(targetFolder);
            SelectedFolder = targetFolder;
        }

        var importedItems = await _mediator.Send(new ImportMarkdownFilesFeature.Command(files, targetFolder.Id));

        if (importedItems != null && importedItems.Any())
        {
            foreach (var item in importedItems)
            {
                Files.Add(item);
            }

            UpdateFilesViewFilter();
            FilesView?.Refresh();
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task DeleteFile(PromptItem? file)
    {
        if (file == null) return;
        
        var result = await _deleteFileHandler.Handle(
            new DeleteFileFeature.Command(file, Files),
            default);

        if (result.Success)
        {
            if (result.WasSelected) SelectedFile = null;
            RequestSave();
        }
    }

    [RelayCommand]
    private async Task ChangeFileIcon(PromptItem? file)
    {
        if (file == null) return;
        
        var result = await _changeFileIconHandler.Handle(
            new ChangeFileIconFeature.Command(file),
            default);

        if (result.Success)
        {
            RequestSave();
        }
    }

    public void MoveFileToFolder(PromptItem f, FolderItem t)
    {
        if (f == null || t == null || f.FolderId == t.Id) return;
        f.FolderId = t.Id;
        FilesView?.Refresh();
        if (SelectedFile == f) SelectedFile = null;
        RequestSave();
    }

    [RelayCommand]
    public void RequestSave()
    {
        if (!IsDirty) IsDirty = true;
        WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
    }

    public async Task PerformLocalBackupAsync()
    {
        try
        {
            await _localDataService.SaveAsync(Folders, Files);
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to perform local backup", "FileManagerViewModel.PerformLocalBackupAsync");
        }
    }

    public async Task PerformCloudBackupAsync()
    {
        try
        {
            await _dataService.SaveAsync(Folders, Files);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to perform cloud backup", "FileManagerViewModel.PerformCloudBackupAsync");
            throw;
        }
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

    private sealed class FileManagerFolderDropHandler : IDropTarget
    {
        private readonly FileManagerViewModel _vm;
        
        public FileManagerFolderDropHandler(FileManagerViewModel vm) => _vm = vm;

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is PromptItem && dropInfo.TargetItem is FolderItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move;
            }
            else if (dropInfo.Data is FolderItem && dropInfo.TargetItem is FolderItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is PromptItem file && dropInfo.TargetItem is FolderItem fileTarget)
            {
                WeakReferenceMessenger.Default.Send(new RequestMoveFileToFolderMessage(file, fileTarget));
                return;
            }

            if (dropInfo.Data is FolderItem sourceFolder && dropInfo.TargetItem is FolderItem)
            {
                int oldIndex = _vm.Folders.IndexOf(sourceFolder);
                int newIndex = dropInfo.InsertIndex;
                if (oldIndex < newIndex) newIndex--;
                if (oldIndex == newIndex) return;
                _vm.ReorderFolders(oldIndex, newIndex);
            }
        }
    }
}
