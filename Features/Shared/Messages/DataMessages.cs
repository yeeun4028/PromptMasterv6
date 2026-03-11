using CommunityToolkit.Mvvm.Messaging.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Shared.Messages;

/// <summary>
/// 当一个文件被选中时发送。
/// </summary>
public class FileSelectedMessage
{
    public PromptItem? File { get; }
    public bool EnterEditMode { get; }

    public FileSelectedMessage(PromptItem? file, bool enterEditMode = false)
    {
        File = file;
        EnterEditMode = enterEditMode;
    }
}

/// <summary>
/// 当数据加载完成并由 FileManager 所有时发送。
/// </summary>
public class DataInitializedMessage
{
    public ObservableCollection<FolderItem> Folders { get; }
    public ObservableCollection<PromptItem> Files { get; }

    public DataInitializedMessage(ObservableCollection<FolderItem> folders, ObservableCollection<PromptItem> files)
    {
        Folders = folders;
        Files = files;
    }
}

/// <summary>
/// 请求执行备份操作。
/// </summary>
public class RequestBackupActionMessage { }
