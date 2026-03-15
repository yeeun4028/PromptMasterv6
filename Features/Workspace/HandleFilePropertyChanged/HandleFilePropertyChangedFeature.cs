using MediatR;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.Workspace.State;
using CommunityToolkit.Mvvm.Messaging;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.HandleFilePropertyChanged;

public static class HandleFilePropertyChangedFeature
{
    public record Command(object? Sender, PropertyChangedEventArgs Args) : IRequest<Result>;
    public record Result(bool ShouldSave);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IWorkspaceState _state;

        public Handler(IWorkspaceState state)
        {
            _state = state;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Args.PropertyName == nameof(PromptItem.LastModified))
            {
                return Task.FromResult(new Result(false));
            }

            if (request.Sender is PromptItem changedItem && 
                request.Args.PropertyName == nameof(PromptItem.Content) && 
                request.Sender == _state.SelectedFile)
            {
                WeakReferenceMessenger.Default.Send(new FileContentChangedMessage(changedItem.Content));
            }

            return Task.FromResult(new Result(true));
        }
    }
}
