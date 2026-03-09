using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Features.Main.Messages;

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
