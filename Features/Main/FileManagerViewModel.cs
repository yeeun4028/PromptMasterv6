using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main.Messages;
using PromptMasterv6.Core.Messages;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PromptMasterv6.Features.Main;

public partial class FileManagerViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDataService _localDataService;
    private readonly DialogService _dialogService;
    private readonly LoggerService _logger;
    private readonly IMediator _mediator;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;
    [ObservableProperty] private ObservableCollection<PromptItem> files = new();
    [ObservableProperty] private PromptItem? selectedFile;
    [ObservableProperty] private ICollectionView? filesView;
    [ObservableProperty] private bool isDirty;

    public event Action? SaveRequested;
    public event Action<PromptItem?>? SelectedFileChanged;

    public FileManagerViewModel(
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService,
        DialogService dialogService,
        LoggerService logger,
        IMediator mediator)
    {
        _dataService = dataService;
        _localDataService = localDataService;
        _dialogService = dialogService;
        _logger = logger;
        _mediator = mediator;

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, (_, _) =>
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
        });

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            SelectedFile = m.File;
        });

        WeakReferenceMessenger.Default.Register<RequestMoveFileToFolderMessage>(this, (_, m) => MoveFileToFolder(m.File, m.TargetFolder));

        WeakReferenceMessenger.Default.Register<RequestPromptFileMessage>(this, (_, m) =>
        {
            var file = Files.FirstOrDefault(f => f.Id == m.PromptId);
            m.Reply(new PromptFileResponseMessage { File = file });
        });
    }

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        SelectedFileChanged?.Invoke(value);
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
    private void RenameFile(PromptItem? item)
    {
        if (item != null)
        {
            item.IsRenaming = true;
        }
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

        var importedItems = await _mediator.Send(new Import.ImportMarkdownFilesCommand(files, targetFolder.Id));

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
    private void DeleteFile(PromptItem? file)
    {
        if (file == null) return;
        Files.Remove(file);
        if (SelectedFile == file) SelectedFile = null;
        RequestSave();
    }

    [RelayCommand]
    private void ChangeFileIcon(PromptItem? file)
    {
        if (file == null) return;
        var dialog = new IconInputDialog(file.IconGeometry);
        if (dialog.ShowDialog() == true)
        {
            file.IconGeometry = dialog.ResultGeometry;
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
        SaveRequested?.Invoke();
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
}
