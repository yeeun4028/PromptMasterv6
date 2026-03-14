using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace.State;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CreateFolder
{
    public partial class CreateFolderViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly IWorkspaceState _state;

        public CreateFolderViewModel(IMediator mediator, IWorkspaceState state)
        {
            _mediator = mediator;
            _state = state;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            var result = await _mediator.Send(new CreateFolderFeature.Command(_state.Folders));
            
            if (result.CreatedFolder != null)
            {
                _state.SelectedFolder = result.CreatedFolder;
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
