using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ChangeFolderIcon
{
    public partial class ChangeFolderIconViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        public ChangeFolderIconViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync(FolderItem? folder)
        {
            if (folder == null) return;
            
            var result = await _mediator.Send(new ChangeFolderIconFeature.Command(folder));
            
            if (result.Success)
            {
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
