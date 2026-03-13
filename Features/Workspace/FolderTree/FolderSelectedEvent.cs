using MediatR;

namespace PromptMasterv6.Features.Workspace.FolderTree;

public sealed record FolderSelectedEvent(string FolderId) : INotification;
