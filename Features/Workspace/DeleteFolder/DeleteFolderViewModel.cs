using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.Messages;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.DeleteFolder
{
    public partial class DeleteFolderViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private FolderItem? _folder;

        [ObservableProperty]
        private ObservableCollection<FolderItem>? _folders;

        [ObservableProperty]
        private ObservableCollection<PromptItem>? _files;

        [ObservableProperty]
        private FolderItem? _selectedFolder;

        public DeleteFolderViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (Folder == null || Folders == null) return;
            
            var result = await _mediator.Send(new DeleteFolderFeature.Command(Folder, Folders, Files));
            
            if (result.Success)
            {
                if (result.WasSelected) SelectedFolder = null;
                WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(null, EnterEditMode: false));
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
