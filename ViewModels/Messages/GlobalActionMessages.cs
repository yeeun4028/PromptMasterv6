using CommunityToolkit.Mvvm.Messaging.Messages;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.ViewModels.Messages;

public class OpenSettingsMessage
{
    public int TabIndex { get; set; } = 0;
}

public class RequestBackupMessage { }

public class ReloadDataMessage { }

public class TriggerTranslateMessage { }

public class TriggerOcrMessage { }

public class TriggerLauncherMessage { }

public class TriggerPinToScreenMessage { }

public class ToggleWindowMessage { }

public class RefreshExternalToolsMessage { }

public class RequestPromptFileMessage : RequestMessage<PromptFileResponseMessage>
{
    public string? PromptId { get; set; }
}

public class PromptFileResponseMessage
{
    public PromptItem? File { get; set; }
}

public class JumpToEditPromptMessage
{
    public PromptItem? File { get; set; }
}

public class NavigationMessage
{
    public object? TargetViewModel { get; set; }
}
