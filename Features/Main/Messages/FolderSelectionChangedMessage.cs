using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Features.Main.Messages;

public sealed record FolderSelectionChangedMessage(FolderItem? Folder);
