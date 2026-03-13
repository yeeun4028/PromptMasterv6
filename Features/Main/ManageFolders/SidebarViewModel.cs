using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Main.Backup.Messages;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using DragDropEffects = System.Windows.DragDropEffects;

namespace PromptMasterv6.Features.Main.Sidebar;

public partial class SidebarViewModel : ObservableObject, IDisposable
{
    private readonly IMediator _mediator;
    private readonly SettingsService _settingsService;
    private readonly LoggerService _logger;
    private readonly DialogService _dialogService;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private LocalSettings localConfig;

    public IDropTarget FolderDropHandler { get; }

    public SidebarViewModel(
        IMediator mediator,
        SettingsService settingsService,
        LoggerService logger,
        DialogService dialogService)
    {
        _mediator = mediator;
        _settingsService = settingsService;
        _logger = logger;
        _dialogService = dialogService;

        LocalConfig = settingsService.LocalConfig;
        FolderDropHandler = new SidebarFolderDropHandler(this);

        WeakReferenceMessenger.Default.Register<DataInitializedMessage>(this, (_, m) =>
        {
            Folders = m.Folders;
            Folders.CollectionChanged += OnFoldersCollectionChanged;
            SelectedFolder = Folders.FirstOrDefault();
        });

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, (_, m) =>
        {
            if (m.Folder != null && Folders.Contains(m.Folder))
            {
                SelectedFolder = m.Folder;
            }
        });

        WeakReferenceMessenger.Default.Register<RequestBackupActionMessage>(this, (_, _) =>
        {
            IsDirty = true;
        });

        WeakReferenceMessenger.Default.Register<BackupCompletedMessage>(this, (_, m) =>
        {
            if (m.Success)
            {
                IsDirty = false;
            }
        });

        WeakReferenceMessenger.Default.Register<ContentEditor.Messages.EditModeChangedMessage>(this, (_, m) =>
        {
            IsEditMode = m.IsEditMode;
        });
    }

    partial void OnSelectedFolderChanged(FolderItem? value)
    {
        WeakReferenceMessenger.Default.Send(new FolderSelectionChangedMessage(value));
    }

    [RelayCommand]
    private void CreateFile()
    {
        WeakReferenceMessenger.Default.Send(new CreateFileRequestMessage(SelectedFolder));
    }

    [RelayCommand]
    private void CreateFolder()
    {
        var f = new FolderItem { Name = $"新建文件夹 {Folders.Count + 1}" };
        Folders.Add(f);
        SelectedFolder = f;
        WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
    }

    [RelayCommand]
    private void ImportMarkdownFiles()
    {
        WeakReferenceMessenger.Default.Send(new ImportMarkdownFilesRequestMessage(SelectedFolder));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsRequestMessage());
    }

    [RelayCommand]
    private async Task ChangeActionIcon(string actionKey)
    {
        if (string.IsNullOrWhiteSpace(actionKey)) return;

        var currentIcon = LocalConfig.ActionIcons != null && LocalConfig.ActionIcons.TryGetValue(actionKey, out var icon) ? icon : "";

        var newIcon = _dialogService.ShowIconInputDialog(currentIcon);
        
        if (newIcon != null)
        {
            var result = await _mediator.Send(
                new ChangeActionIconFeature.Command(actionKey, newIcon));
            
            if (result.Success)
            {
                OnPropertyChanged(nameof(LocalConfig));
            }
        }
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        WeakReferenceMessenger.Default.Send(new ToggleEditModeRequestMessage());
    }

    [RelayCommand]
    private void ManualBackup()
    {
        WeakReferenceMessenger.Default.Send(new RequestCloudBackupMessage());
    }

    [RelayCommand]
    private void ChangeFolderIcon(FolderItem? folder)
    {
        WeakReferenceMessenger.Default.Send(new ChangeFolderIconRequestMessage(folder));
    }

    [RelayCommand]
    private void RenameFolder(FolderItem? folder)
    {
        WeakReferenceMessenger.Default.Send(new RenameFolderRequestMessage(folder));
    }

    [RelayCommand]
    private void DeleteFolder(FolderItem? folder)
    {
        WeakReferenceMessenger.Default.Send(new DeleteFolderRequestMessage(folder));
    }

    public void ReorderFolders(int oldIndex, int newIndex)
    {
        Folders.Move(oldIndex, newIndex);
        WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
    }

    private void OnFoldersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
    }

    public void Dispose()
    {
        if (Folders != null)
        {
            Folders.CollectionChanged -= OnFoldersCollectionChanged;
        }
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private sealed class SidebarFolderDropHandler : IDropTarget
    {
        private readonly SidebarViewModel _vm;
        
        public SidebarFolderDropHandler(SidebarViewModel vm) => _vm = vm;

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is FolderItem && dropInfo.TargetItem is FolderItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
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
