
namespace PromptMasterv6.Features.Main.Messages;

public sealed record RequestSelectFileMessage(PromptItem? File, bool EnterEditMode = false);
