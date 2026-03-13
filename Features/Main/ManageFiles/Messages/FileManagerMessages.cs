using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Main.FileManager.Messages;

public sealed record RequestSelectFileMessage(PromptItem? File, bool EnterEditMode = false);

public sealed record RequestMoveFileToFolderMessage(PromptItem File, FolderItem TargetFolder);

public class RequestPromptFileMessage : CommunityToolkit.Mvvm.Messaging.Messages.RequestMessage<PromptFileResponseMessage>
{
    public string? PromptId { get; set; }
}

public class PromptFileResponseMessage
{
    public PromptItem? File { get; set; }
}

public class JumpToEditPromptMessage
{
    public PromptItem? File { get; set; }
}
