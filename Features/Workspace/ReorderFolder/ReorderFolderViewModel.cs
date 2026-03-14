using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.ReorderFolder
{
    public partial class ReorderFolderViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private ObservableCollection<FolderItem>? _folders;

        public ReorderFolderViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task ReorderAsync(int oldIndex, int newIndex)
        {
            if (Folders == null) return;
            
            var result = await _mediator.Send(new ReorderFolderFeature.Command(Folders, oldIndex, newIndex));
            
            if (result.Success)
            {
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
