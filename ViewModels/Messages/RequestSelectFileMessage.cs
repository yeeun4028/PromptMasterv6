using PromptMasterv6.Core.Models;

namespace PromptMasterv6.ViewModels.Messages
{
    public sealed record RequestSelectFileMessage(PromptItem? File, bool EnterEditMode);
}

