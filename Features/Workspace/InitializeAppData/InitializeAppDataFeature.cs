using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.LoadAppData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.InitializeAppData;

public static class InitializeAppDataFeature
{
    public record Command() : IRequest<Result>;

    public record Result(
        bool Success,
        string Message,
        ObservableCollection<FolderItem> Folders,
        ObservableCollection<PromptItem> Files,
        FolderItem? SelectedFolder,
        PromptItem? SelectedFile,
        bool UsedDefaultData = false
    );

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var loadResult = await _mediator.Send(new LoadAppDataFeature.Command(), cancellationToken);

            AppData data;
            bool usedDefaultData = false;

            if (loadResult.Success && loadResult.Data != null)
            {
                data = loadResult.Data;
                usedDefaultData = loadResult.UsedDefaultData;
            }
            else
            {
                data = new AppData();
            }

            var files = new ObservableCollection<PromptItem>(data.Files ?? new List<PromptItem>());
            var folders = new ObservableCollection<FolderItem>(data.Folders ?? new List<FolderItem>());

            FolderItem? selectedFolder;
            if (folders.Count == 0)
            {
                var defaultFolder = new FolderItem { Name = "默认" };
                folders.Add(defaultFolder);
                selectedFolder = defaultFolder;
            }
            else
            {
                selectedFolder = folders.FirstOrDefault();
            }

            if (selectedFolder != null)
            {
                foreach (var f in files)
                {
                    if (string.IsNullOrWhiteSpace(f.FolderId))
                    {
                        f.FolderId = selectedFolder.Id;
                    }
                }
            }

            PromptItem? selectedFile = files.FirstOrDefault();

            return new Result(
                loadResult.Success,
                loadResult.Message,
                folders,
                files,
                selectedFolder,
                selectedFile,
                usedDefaultData
            );
        }
    }
}
