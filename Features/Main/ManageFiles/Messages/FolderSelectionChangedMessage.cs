using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Main.FileManager.Messages;

public sealed record FolderSelectionChangedMessage(FolderItem? Folder);
