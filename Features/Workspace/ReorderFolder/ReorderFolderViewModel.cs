using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace.State;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ReorderFolder
{
    public partial class ReorderFolderViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly IWorkspaceState _state;

        public ReorderFolderViewModel(IMediator mediator, IWorkspaceState state)
        {
            _mediator = mediator;
            _state = state;
        }

        public async Task ReorderAsync(int oldIndex, int newIndex)
        {
            var result = await _mediator.Send(new ReorderFolderFeature.Command(_state.Folders, oldIndex, newIndex));
            
            if (result.Success)
            {
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
