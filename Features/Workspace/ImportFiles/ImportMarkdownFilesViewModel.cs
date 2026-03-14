using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.SelectFilesForImport;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PromptMasterv6.Features.Workspace.ImportFiles
{
    public partial class ImportMarkdownFilesViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private ObservableCollection<FolderItem>? _folders;

        [ObservableProperty]
        private ObservableCollection<PromptItem>? _files;

        [ObservableProperty]
        private FolderItem? _selectedFolder;

        [ObservableProperty]
        private ICollectionView? _filesView;

        public ImportMarkdownFilesViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            var selectResult = await _mediator.Send(new SelectFilesForImportFeature.Command());
            
            if (!selectResult.Success || selectResult.Files == null || selectResult.Files.Length == 0) 
                return;

            string? targetFolderId = SelectedFolder?.Id;

            if (string.IsNullOrEmpty(targetFolderId))
            {
                var newFolder = new FolderItem 
                { 
                    Id = System.Guid.NewGuid().ToString("N"),
                    Name = "导入" 
                };
                Folders?.Add(newFolder);
                SelectedFolder = newFolder;
                targetFolderId = newFolder.Id;
            }

            var importResult = await _mediator.Send(new ImportMarkdownFilesFeature.Command(selectResult.Files, targetFolderId));

            if (importResult.Success && importResult.ImportedItems.Count > 0)
            {
                foreach (var item in importResult.ImportedItems)
                {
                    Files?.Add(item);
                }

                UpdateFilesViewFilter();
                FilesView?.Refresh();
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }

        private void UpdateFilesViewFilter()
        {
            if (FilesView == null) return;

            var selectedFolderId = SelectedFolder?.Id;
            FilesView.Filter = item =>
            {
                if (item is not PromptItem f) return false;
                if (string.IsNullOrWhiteSpace(selectedFolderId)) return true;
                return string.Equals(f.FolderId, selectedFolderId, System.StringComparison.Ordinal);
            };
        }
    }
}
