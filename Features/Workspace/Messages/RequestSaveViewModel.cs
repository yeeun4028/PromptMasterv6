using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Workspace.State;

namespace PromptMasterv6.Features.Workspace.Messages
{
    public partial class RequestSaveViewModel : ObservableObject
    {
        private readonly IWorkspaceState _state;

        public RequestSaveViewModel(IWorkspaceState state)
        {
            _state = state;
        }

        [RelayCommand]
        private void Execute()
        {
            if (!_state.IsDirty) _state.IsDirty = true;
            WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
        }
    }
}
