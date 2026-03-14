using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.SelectFilesForImport;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ImportFiles
{
    public partial class ImportMarkdownFilesViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly IWorkspaceState _state;

        public ImportMarkdownFilesViewModel(IMediator mediator, IWorkspaceState state)
        {
            _mediator = mediator;
            _state = state;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            var selectResult = await _mediator.Send(new SelectFilesForImportFeature.Command());
            
            if (!selectResult.Success || selectResult.Files == null || selectResult.Files.Length == 0) 
                return;

            string? targetFolderId = _state.SelectedFolder?.Id;

            if (string.IsNullOrEmpty(targetFolderId))
            {
                var newFolder = new FolderItem 
                { 
                    Id = System.Guid.NewGuid().ToString("N"),
                    Name = "导入" 
                };
                _state.Folders.Add(newFolder);
                _state.SelectedFolder = newFolder;
                targetFolderId = newFolder.Id;
            }

            var importResult = await _mediator.Send(new ImportMarkdownFilesFeature.Command(selectResult.Files, targetFolderId));

            if (importResult.Success && importResult.ImportedItems.Count > 0)
            {
                foreach (var item in importResult.ImportedItems)
                {
                    _state.Files.Add(item);
                }

                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
