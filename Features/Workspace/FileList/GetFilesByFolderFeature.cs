using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.FileList;

public static class GetFilesByFolderFeature
{
    public class Handler : IRequestHandler<GetFilesByFolderQuery, List<PromptItem>>
    {
        private readonly IWorkspaceState _state;

        public Handler(IWorkspaceState state)
        {
            _state = state;
        }

        public Task<List<PromptItem>> Handle(GetFilesByFolderQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.FolderId))
            {
                return Task.FromResult(_state.Files.ToList());
            }

            var filteredFiles = _state.Files
                .Where(f => string.Equals(f.FolderId, request.FolderId, StringComparison.Ordinal))
                .ToList();

            return Task.FromResult(filteredFiles);
        }
    }
}
