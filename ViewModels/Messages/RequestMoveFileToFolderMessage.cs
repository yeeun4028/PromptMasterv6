using PromptMasterv6.Core.Models;

namespace PromptMasterv6.ViewModels.Messages
{
    public sealed record RequestMoveFileToFolderMessage(PromptItem File, FolderItem TargetFolder);
}

