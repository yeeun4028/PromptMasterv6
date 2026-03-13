using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.FilterFiles;

public static class FilterFilesFeature
{
    public record Command(
        ICollectionView? FilesView,
        FolderItem? SelectedFolder) : IRequest;

    public class Handler : IRequestHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.FilesView == null)
            {
                return Task.CompletedTask;
            }

            var selectedFolderId = request.SelectedFolder?.Id;
            request.FilesView.Filter = item =>
            {
                if (item is not PromptItem f) return false;
                if (string.IsNullOrWhiteSpace(selectedFolderId)) return true;
                return string.Equals(f.FolderId, selectedFolderId, StringComparison.Ordinal);
            };

            return Task.CompletedTask;
        }
    }
}
