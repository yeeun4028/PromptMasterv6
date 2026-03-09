using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PromptMasterv6.Features.Shared.Models;

public partial class PromptItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? content;

    [ObservableProperty]
    private DateTime lastModified;

    public string? FolderId { get; set; }

    [ObservableProperty]
    private string? iconGeometry;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private DateTime createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime updatedAt = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore]
    [ObservableProperty]
    private bool isRenaming;
}
