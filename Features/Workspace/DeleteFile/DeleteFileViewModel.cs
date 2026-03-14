using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.DeleteFile
{
    public partial class DeleteFileViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly IWorkspaceState _state;

        public DeleteFileViewModel(IMediator mediator, IWorkspaceState state)
        {
            _mediator = mediator;
            _state = state;
        }

        [RelayCommand]
        private async Task ExecuteAsync(PromptItem? file)
        {
            if (file == null) return;
            
            var result = await _mediator.Send(new DeleteFileFeature.Command(file, _state.Files));
            
            if (result.Success)
            {
                if (result.WasSelected) _state.SelectedFile = null;
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
