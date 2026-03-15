using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.FilterFiles;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PromptMasterv6.Features.Workspace.SetFiles;

public static class SetFilesFeature
{
    public record Command(ObservableCollection<PromptItem> Files) : IRequest<Result>;
    public record Result(bool Success);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IWorkspaceState _state;
        private readonly IMediator _mediator;

        public Handler(IWorkspaceState state, IMediator mediator)
        {
            _state = state;
            _mediator = mediator;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            foreach (var item in _state.Files)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }

            _state.Files.Clear();
            foreach (var file in request.Files)
            {
                _state.Files.Add(file);
            }

            foreach (var item in _state.Files)
            {
                item.PropertyChanged += OnFilePropertyChanged;
            }

            _state.FilesView = CollectionViewSource.GetDefaultView(_state.Files);
            _ = _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder), cancellationToken);

            return Task.FromResult(new Result(true));
        }

        private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
        }
    }
}
