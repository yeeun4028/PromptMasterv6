using MediatR;

namespace PromptMasterv6.Features.Shared.Events;

public sealed record BackupActionRequestedEvent : INotification;

public sealed record BackupRequestedEvent : INotification;

public sealed record CloudBackupRequestedEvent : INotification;

public sealed record BackupCompletedEvent(bool Success, string? ErrorMessage = null) : INotification;
