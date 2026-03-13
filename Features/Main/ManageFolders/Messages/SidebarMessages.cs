using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Main.Sidebar.Messages
{
    public class CreateFileRequestMessage
    {
        public FolderItem? TargetFolder { get; }

        public CreateFileRequestMessage(FolderItem? targetFolder)
        {
            TargetFolder = targetFolder;
        }
    }

    public class ImportMarkdownFilesRequestMessage
    {
        public FolderItem? TargetFolder { get; }

        public ImportMarkdownFilesRequestMessage(FolderItem? targetFolder)
        {
            TargetFolder = targetFolder;
        }
    }

    public class OpenSettingsRequestMessage
    {
    }

    public class ToggleEditModeRequestMessage
    {
    }

    public class ChangeFolderIconRequestMessage
    {
        public FolderItem? Folder { get; }

        public ChangeFolderIconRequestMessage(FolderItem? folder)
        {
            Folder = folder;
        }
    }

    public class RenameFolderRequestMessage
    {
        public FolderItem? Folder { get; }

        public RenameFolderRequestMessage(FolderItem? folder)
        {
            Folder = folder;
        }
    }

    public class DeleteFolderRequestMessage
    {
        public FolderItem? Folder { get; }

        public DeleteFolderRequestMessage(FolderItem? folder)
        {
            Folder = folder;
        }
    }
}
