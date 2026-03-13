using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class ImportMarkdownFilesFeature
{
    public record Command(string[] Files, string? TargetFolderId = null) : IRequest<Result>;

    public record Result(
        bool Success,
        List<PromptItem> ImportedItems,
        int ImportCount,
        string? NewFolderId = null
    );

    public class Handler : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var importedItems = new List<PromptItem>();

            if (request.Files == null || request.Files.Length == 0)
                return new Result(false, importedItems, 0);

            string? targetFolderId = request.TargetFolderId;
            if (string.IsNullOrEmpty(targetFolderId))
            {
                targetFolderId = Guid.NewGuid().ToString("N");
            }

            foreach (var filePath in request.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    var title = Path.GetFileNameWithoutExtension(filePath);
                    
                    var item = new PromptItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Title = title,
                        Content = content,
                        FolderId = targetFolderId,
                        LastModified = DateTime.Now
                    };
                    
                    importedItems.Add(item);
                }
                catch
                {
                }
            }

            bool needsNewFolder = string.IsNullOrEmpty(request.TargetFolderId) && importedItems.Any();
            return new Result(
                true, 
                importedItems, 
                importedItems.Count, 
                needsNewFolder ? targetFolderId : null
            );
        }
    }
}
