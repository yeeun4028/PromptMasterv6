using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.FileManager;

public static class ImportMarkdownFilesFeature
{
    public record Command(string[] Files, string TargetFolderId) : IRequest<List<PromptItem>>;

    public class Handler : IRequestHandler<Command, List<PromptItem>>
    {
        public async Task<List<PromptItem>> Handle(Command request, CancellationToken cancellationToken)
        {
            var importedItems = new List<PromptItem>();

            if (request.Files == null || request.Files.Length == 0)
                return importedItems;

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
                        FolderId = request.TargetFolderId,
                        LastModified = DateTime.Now
                    };
                    
                    importedItems.Add(item);
                }
                catch
                {
                }
            }

            return importedItems;
        }
    }
}
