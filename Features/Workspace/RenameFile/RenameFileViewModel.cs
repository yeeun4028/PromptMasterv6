using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.RenameFile
{
    public partial class RenameFileViewModel : ObservableObject
    {
        private readonly IMediator _mediator;

        public IMediator Mediator => _mediator;

        public RenameFileViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync(PromptItem? item)
        {
            if (item == null) return;
            await _mediator.Send(new RenameFileFeature.Command(item));
        }
    }
}
