using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PromptMasterv6.Features.Shared.Models;

public partial class FolderItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? iconGeometry;
}
