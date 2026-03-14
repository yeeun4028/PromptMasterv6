using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PromptMasterv6.Features.Workspace.MoveFile
{
    public partial class MoveFileViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private PromptItem? _file;

        [ObservableProperty]
        private FolderItem? _targetFolder;

        [ObservableProperty]
        private PromptItem? _selectedFile;

        [ObservableProperty]
        private ICollectionView? _filesView;

        public MoveFileViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (File == null || TargetFolder == null) return;
            
            var result = await _mediator.Send(new MoveFileToFolderFeature.Command(File, TargetFolder));
            
            if (result.Success)
            {
                FilesView?.Refresh();
                if (SelectedFile == File) SelectedFile = null;
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }
}
