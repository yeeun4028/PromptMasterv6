using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using DragDropEffects = System.Windows.DragDropEffects;

namespace PromptMasterv5.ViewModels
{
    public partial class SidebarViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public IDropTarget FolderDropHandler { get; }

        [ObservableProperty]
        private ObservableCollection<FolderItem> folders = new();

        [ObservableProperty]
        private FolderItem? selectedFolder;

        public ObservableCollection<PromptItem>? Files { get; set; }
        public Func<PromptItem?>? GetSelectedFile { get; set; }
        public Action<PromptItem?>? SelectFile { get; set; }
        public Action<bool>? SetEditMode { get; set; }
        public Action? RequestSave { get; set; }
        public Action<PromptItem, FolderItem>? MoveFileToFolder { get; set; }

        public SidebarViewModel(IDataService dataService)
        {
            _dataService = dataService;
            FolderDropHandler = new SidebarFolderDropHandler(this);
        }

        [RelayCommand]
        private void CreateFolder()
        {
            var f = new FolderItem { Name = $"新建文件夹 {Folders.Count + 1}" };
            Folders.Add(f);
            SelectedFolder = f;
            RequestSave?.Invoke();
        }

        [RelayCommand]
        private void CreateFile()
        {
            if (SelectedFolder == null) return;
            if (Files == null) return;

            var f = new PromptItem { Title = "", Content = "", FolderId = SelectedFolder.Id, LastModified = DateTime.Now };
            Files.Add(f);
            SelectFile?.Invoke(f);
            SetEditMode?.Invoke(true);
            RequestSave?.Invoke();
        }

        [RelayCommand]
        private void DeleteFolder(FolderItem? folder)
        {
            if (folder == null) return;

            if (Files != null)
            {
                var filesInFolder = Files.Where(f => f.FolderId == folder.Id).ToList();
                foreach (var file in filesInFolder) Files.Remove(file);
            }

            if (SelectedFolder == folder) SelectedFolder = null;
            var selectedFile = GetSelectedFile?.Invoke();
            if (selectedFile != null && selectedFile.FolderId == folder.Id)
            {
                SelectFile?.Invoke(null);
            }
            Folders.Remove(folder);
            RequestSave?.Invoke();
        }

        [RelayCommand]
        private void ChangeFolderIcon(FolderItem f)
        {
            if (f == null) return;
            var dialog = new IconInputDialog(f.IconGeometry);
            if (dialog.ShowDialog() == true)
            {
                f.IconGeometry = dialog.ResultGeometry;
                RequestSave?.Invoke();
            }
        }

        [RelayCommand]
        private void RenameFolder(FolderItem f)
        {
            if (f == null) return;
            var dialog = new NameInputDialog(f.Name);
            if (dialog.ShowDialog() == true)
            {
                f.Name = dialog.ResultName;
                RequestSave?.Invoke();
            }
        }

        public void ReorderFolders(int o, int n)
        {
            Folders.Move(o, n);
            RequestSave?.Invoke();
        }

        public async Task LoadDataAsync()
        {
            var data = await _dataService.LoadAsync();
            if (data == null) return;

            SelectedFolder = null;
            Folders.Clear();
            foreach (var f in data.Folders) Folders.Add(f);
            if (Folders.Count > 0) SelectedFolder = Folders.FirstOrDefault();
        }

        private sealed class SidebarFolderDropHandler : IDropTarget
        {
            private readonly SidebarViewModel _vm;

            public SidebarFolderDropHandler(SidebarViewModel vm)
            {
                _vm = vm;
            }

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
                    if (_vm.MoveFileToFolder != null)
                    {
                        _vm.MoveFileToFolder(file, fileTarget);
                    }
                    else
                    {
                        if (file.FolderId == fileTarget.Id) return;
                        file.FolderId = fileTarget.Id;
                        _vm.RequestSave?.Invoke();
                    }
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
}
