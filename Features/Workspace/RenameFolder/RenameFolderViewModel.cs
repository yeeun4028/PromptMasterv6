using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.RenameFolder
{
    public partial class RenameFolderViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private FolderItem? _folder;

        public RenameFolderViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (Folder == null) return;
            
            var result = await _mediator.Send(new RenameFolderFeature.Command(Folder));
            
            if (result.Success)
            {
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
