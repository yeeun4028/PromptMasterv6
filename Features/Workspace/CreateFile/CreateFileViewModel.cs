using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.Messages;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CreateFile
{
    public partial class CreateFileViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly IWorkspaceState _state;

        public CreateFileViewModel(IMediator mediator, IWorkspaceState state)
        {
            _mediator = mediator;
            _state = state;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (_state.SelectedFolder == null) return;
            
            var result = await _mediator.Send(new CreateFileFeature.Command(_state.SelectedFolder.Id));
            
            if (result.CreatedFile != null)
            {
                _state.Files.Add(result.CreatedFile);
                WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(result.CreatedFile, EnterEditMode: true));
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
