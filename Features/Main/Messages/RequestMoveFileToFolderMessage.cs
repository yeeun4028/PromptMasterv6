using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Features.Main.Messages;

public sealed record RequestMoveFileToFolderMessage(PromptItem File, FolderItem TargetFolder);
