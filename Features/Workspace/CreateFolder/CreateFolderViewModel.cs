using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CreateFolder
{
    public partial class CreateFolderViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private ObservableCollection<FolderItem>? _folders;

        [ObservableProperty]
        private FolderItem? _createdFolder;

        public CreateFolderViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (Folders == null) return;
            
            var result = await _mediator.Send(new CreateFolderFeature.Command(Folders));
            
            if (result.CreatedFolder != null)
            {
                CreatedFolder = result.CreatedFolder;
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
