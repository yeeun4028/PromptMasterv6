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
}
