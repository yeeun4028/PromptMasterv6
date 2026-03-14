using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.MoveFile
{
    public partial class MoveFileViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly IWorkspaceState _state;

        public MoveFileViewModel(IMediator mediator, IWorkspaceState state)
        {
            _mediator = mediator;
            _state = state;
        }

        [RelayCommand]
        private async Task ExecuteAsync(MoveFileParameter? param)
        {
            if (param?.File == null || param?.TargetFolder == null) return;
            
            var result = await _mediator.Send(new MoveFileToFolderFeature.Command(param.File, param.TargetFolder));
            
            if (result.Success)
            {
                if (_state.SelectedFile == param.File) _state.SelectedFile = null;
                WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
            }
        }
    }

    public record MoveFileParameter(PromptItem File, FolderItem TargetFolder);
}
