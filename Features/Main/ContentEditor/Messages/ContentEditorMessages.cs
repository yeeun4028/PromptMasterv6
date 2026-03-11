using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Main.ContentEditor.Messages;

public class ContentChangedMessage
{
    public PromptItem ChangedFile { get; }

    public ContentChangedMessage(PromptItem changedFile)
    {
        ChangedFile = changedFile;
    }
}

public class EditModeChangedMessage
{
    public bool IsEditMode { get; }

    public EditModeChangedMessage(bool isEditMode)
    {
        IsEditMode = isEditMode;
    }
}
