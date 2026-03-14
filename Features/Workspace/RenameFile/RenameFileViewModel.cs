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

        [ObservableProperty]
        private PromptItem? _item;

        public RenameFileViewModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (Item == null) return;
            await _mediator.Send(new RenameFileFeature.Command(Item));
        }
    }
}
