using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Workspace.State;

public class WorkspaceState : IWorkspaceState
{
    public ObservableCollection<FolderItem> Folders { get; } = new();
    public ObservableCollection<PromptItem> Files { get; } = new();
    public FolderItem? SelectedFolder { get; set; }
    public PromptItem? SelectedFile { get; set; }
    public bool IsDirty { get; set; }

    public void Initialize(ObservableCollection<FolderItem> folders, ObservableCollection<PromptItem> files)
    {
        Folders.Clear();
        foreach (var folder in folders)
        {
            Folders.Add(folder);
        }

        Files.Clear();
        foreach (var file in files)
        {
            Files.Add(file);
        }
    }
}
