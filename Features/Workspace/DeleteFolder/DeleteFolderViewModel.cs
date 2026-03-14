using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.Messages;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.DeleteFolder
{
    public partial class DeleteFolderViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly IWorkspaceState _state;

        public DeleteFolderViewModel(IMediator mediator, IWorkspaceState state)
        {
            _mediator = mediator;
            _state = state;
        }

        [RelayCommand]
        private async Task ExecuteAsync(FolderItem? folder)
        {
            if (folder == null) return;
            
            var result = await _mediator.Send(new DeleteFolderFeature.Command(folder, _state.Folders, _state.Files));
            
            if (result.Success)
            {
                if (result.WasSelected) _state.SelectedFolder = null;
                WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(null, EnterEditMode: false));
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
