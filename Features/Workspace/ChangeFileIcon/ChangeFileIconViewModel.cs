using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ChangeFileIcon
{
    public partial class ChangeFileIconViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        public ChangeFileIconViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync(PromptItem? item)
        {
            if (item == null) return;
            
            var result = await _mediator.Send(new ChangeFileIconFeature.Command(item));
            
            if (result.Success && result.NewIconGeometry != null)
            {
                item.IconGeometry = result.NewIconGeometry;
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
