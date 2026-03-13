using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Workspace.Messages;

public sealed record FolderSelectionChangedMessage(FolderItem? Folder);
