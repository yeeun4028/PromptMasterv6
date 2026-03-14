using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Features.Shared.Messages;

namespace PromptMasterv6.Features.Workspace.Messages
{
    public partial class RequestSaveViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isDirty;

        [RelayCommand]
        private void Execute()
        {
            if (!IsDirty) IsDirty = true;
            WeakReferenceMessenger.Default.Send(new RequestBackupActionMessage());
        }
    }
}
