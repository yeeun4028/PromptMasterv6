using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Shared.Models;

public interface ISessionState
{
    ObservableCollection<FolderItem> Folders { get; }
    ObservableCollection<PromptItem> Files { get; }
    FolderItem? SelectedFolder { get; set; }
    LocalSettings LocalConfig { get; }
    bool IsDirty { get; set; }
    bool IsEditMode { get; set; }
    
    void RefreshFilesView();
}

public class SessionState : ISessionState
{
    public ObservableCollection<FolderItem> Folders { get; } = new();
    public ObservableCollection<PromptItem> Files { get; } = new();
    public FolderItem? SelectedFolder { get; set; }
    public LocalSettings LocalConfig { get; } = new();
    public bool IsDirty { get; set; }
    public bool IsEditMode { get; set; }
    
    public event Action? FilesViewRefreshRequested;
    
    public void RefreshFilesView()
    {
        FilesViewRefreshRequested?.Invoke();
    }
}
