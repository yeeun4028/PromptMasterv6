using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Main.ManageFiles.Messages;

public sealed record FolderSelectionChangedMessage(FolderItem? Folder);
