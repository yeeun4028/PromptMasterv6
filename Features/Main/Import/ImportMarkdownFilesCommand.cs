using MediatR;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Import;

public record ImportMarkdownFilesCommand(
    string[]? Files,
    FolderItem? SelectedFolder,
    ObservableCollection<FolderItem> Folders) : IRequest<FolderItem?>;

public record ShowImportFileDialogCommand() : IRequest<string[]?>;

public class ImportMarkdownFilesHandler : IRequestHandler<ImportMarkdownFilesCommand, FolderItem?>
{
    public Task<FolderItem?> Handle(ImportMarkdownFilesCommand request, CancellationToken cancellationToken)
    {
        if (request.Files == null || request.Files.Length == 0)
            return Task.FromResult<FolderItem?>(null);

        var targetFolder = request.SelectedFolder;
        if (targetFolder == null)
        {
            targetFolder = new FolderItem { Name = "导入" };
            request.Folders.Add(targetFolder);
        }

        foreach (var filePath in request.Files)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var title = Path.GetFileNameWithoutExtension(filePath);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
            }
        }

        return Task.FromResult<FolderItem?>(targetFolder);
    }
}

public class ShowImportFileDialogHandler : IRequestHandler<ShowImportFileDialogCommand, string[]?>
{
    private readonly IDialogService _dialogService;

    public ShowImportFileDialogHandler(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public Task<string[]?> Handle(ShowImportFileDialogCommand request, CancellationToken cancellationToken)
    {
        string filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*";
        var files = _dialogService.ShowOpenFilesDialog(filter);
        return Task.FromResult(files);
    }
}
