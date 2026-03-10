using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PromptMasterv6.Features.Launcher;

public enum LauncherCategory
{
    Bookmark,
    Application,
    Tool
}

public partial class LauncherItem : ObservableObject
{
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string? iconGeometry;
    [ObservableProperty] private string? iconPath;
    [ObservableProperty] private string? filePath;
    [ObservableProperty] private string? customImagePath;
    [ObservableProperty] private Action? action;
    [ObservableProperty] private bool runAsAdmin;
    [ObservableProperty] private int displayOrder;
    [ObservableProperty] private LauncherCategory category = LauncherCategory.Application;
}
