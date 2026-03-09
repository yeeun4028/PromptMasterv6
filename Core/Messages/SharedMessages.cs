namespace PromptMasterv6.Core.Messages;

public sealed record RequestSaveMessage;
public sealed record RequestBackupMessage;
public sealed record ReloadDataMessage;
public sealed record ToggleWindowMessage;
public sealed record ToggleMainWindowMessage;
public sealed record OpenSettingsMessage(int TabIndex = 0);
public sealed record TriggerTranslateMessage;
public sealed record TriggerOcrMessage;
public sealed record TriggerPinToScreenMessage;
