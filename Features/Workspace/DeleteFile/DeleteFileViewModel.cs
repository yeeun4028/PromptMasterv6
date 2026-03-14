using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.DeleteFile
{
    public partial class DeleteFileViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private PromptItem? _file;

        [ObservableProperty]
        private ObservableCollection<PromptItem>? _files;

        [ObservableProperty]
        private PromptItem? _selectedFile;

        public DeleteFileViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (File == null || Files == null) return;
            
            var result = await _mediator.Send(new DeleteFileFeature.Command(File, Files));
            
            if (result.Success)
            {
                if (result.WasSelected) SelectedFile = null;
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
