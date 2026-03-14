using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.FilesCollectionChanged;

public static class FilesCollectionChangedFeature
{
    public record Command(
        NotifyCollectionChangedEventArgs Args,
        ObservableCollection<PromptItem> Files) : IRequest;

    public class Handler : IRequestHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Args.NewItems != null)
            {
                foreach (PromptItem item in request.Args.NewItems)
                {
                    item.PropertyChanged += (s, e) => { };
                }
            }

            if (request.Args.OldItems != null)
            {
                foreach (PromptItem item in request.Args.OldItems)
                {
                    item.PropertyChanged -= (s, e) => { };
                }
            }

            return Task.CompletedTask;
        }
    }
}
