using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.Messages;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.CreateFile
{
    public partial class CreateFileViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private FolderItem? _selectedFolder;

        [ObservableProperty]
        private ObservableCollection<PromptItem>? _files;

        [ObservableProperty]
        private PromptItem? _createdFile;

        public CreateFileViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (SelectedFolder == null || Files == null) return;
            
            var result = await _mediator.Send(new CreateFileFeature.Command(SelectedFolder.Id));
            
            if (result.CreatedFile != null)
            {
                Files.Add(result.CreatedFile);
                CreatedFile = result.CreatedFile;
                WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(result.CreatedFile, EnterEditMode: true));
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
