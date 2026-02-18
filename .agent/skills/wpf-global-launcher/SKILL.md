---
name: wpf-global-launcher
description: Use when implementing advanced global launchers with auto-discovery, scripting, and admin privileges in WPF.
---

# WPF Global Launcher Pattern

## Overview

This skill provides patterns for creating a sophisticated, "Spotlight-like" or "Alfred-like" launcher in WPF. It covers global hotkeys (Alt+Space), auto-discovery of scripts/apps, hybrid scripting support, and admin privilege management.

## When to Use

- Creating a central command center for an application.
- Replacing simple button clicks with keyboard-driven workflows.
- Enabling users to extend functionality via scripts or configuration files without recompiling.

## Core Architecture

### 1. Auto-Discovery Service
Instead of hardcoding items, scan a directory for plugins, scripts, or shortcuts.

```csharp
public class LauncherItem
{
    public string Title { get; set; }
    public string IconPath { get; set; }
    public string ExecutePath { get; set; }
    public bool RunAsAdmin { get; set; }
    public Action ExecuteAction { get; set; }
}

public IEnumerable<LauncherItem> DiscoverItems(string rootPath)
{
    // 1. Scan Shortcuts (.lnk)
    foreach (var file in Directory.GetFiles(rootPath, "*.lnk"))
    {
        yield return new LauncherItem { 
            Title = Path.GetFileNameWithoutExtension(file),
            ExecutePath = file,
            IconPath = file // Extract later
        };
    }

    // 2. Scan Scripts (.cs, .ps1)
    foreach (var file in Directory.GetFiles(rootPath, "*.cs"))
    {
        yield return new LauncherItem {
            Title = Path.GetFileNameWithoutExtension(file),
            ExecuteAction = () => ExecuteScript(file)
        };
    }
}
```

### 2. Hybrid Scripting (CS-Script / PowerShell)
Support both inline scripts and external files.

**Dependencies:** `CS-Script` (for C#), `System.Management.Automation` (for PowerShell).

```csharp
// Fire & Forget Execution
public void ExecuteScript(string filePath)
{
    if (filePath.EndsWith(".cs"))
    {
        // Use CS-Script to compile and run
        CSScript.Evaluator.LoadFile(filePath);
    }
    else if (filePath.EndsWith(".ps1"))
    {
        Process.Start(new ProcessStartInfo("pwsh", $"-File \"{filePath}\"") { 
            UseShellExecute = false, 
            CreateNoWindow = true 
        });
    }
}
```

### 3. Global Hotkey (Gma.System.MouseKeyHook)
Use `GlobalKeyService` to listen for `Alt+Space` or user-defined hotkeys.

**(See `Implementation` section below for full service code)**

### 4. Admin Privileges
To run items as Administrator:

```csharp
var info = new ProcessStartInfo(exePath)
{
    UseShellExecute = true,
    Verb = "runas" // Triggers UAC prompt
};
try { Process.Start(info); }
catch (Win32Exception) { /* User cancelled UAC */ }
```

### 5. Icon Extraction
Use `System.Drawing.Icon.ExtractAssociatedIcon` for files. For high-res icons from .exe/.lnk, use Windows API (`Shell32.SHGetFileInfo`).

```csharp
// Simple (Low Res)
var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
var bitmap = icon.ToBitmap(); // Convert to WPF ImageSource
```

## UI Patterns: App Grid & Keyboard Nav

### XAML Layout
Use an `ItemsControl` with a `WrapPanel` or `UniformGrid`.

```xml
<ItemsControl ItemsSource="{Binding FilteredItems}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <UniformGrid Columns="4" /> <!-- Or WrapPanel -->
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Button Command="{Binding ExecuteCommand}" ...>
                <StackPanel>
                    <Image Source="{Binding Icon}" Width="32"/>
                    <TextBlock Text="{Binding Title}"/>
                </StackPanel>
            </Button>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### Keyboard Navigation
To support Arrow Keys + Enter:
1.  Focus the first item when window opens.
2.  Use `FocusManager.FocusedElement` to track selection.
3.  Handle `PreviewKeyDown` on the Window to navigate grid if default Tab navigation isn't enough.

## Window Management (Critical)

**Prevent Crashing on Close:**
Launcher windows often crash due to race conditions between `Deactivated` (blur) and explicit `Close()` calls.

**Pattern:**
```csharp
public partial class LauncherWindow : Window
{
    private bool _isClosing = false;
    
    public LauncherWindow()
    {
        InitializeComponent();
        Closing += (s, e) => _isClosing = true; // Mark as closing immediately
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        SafeClose();
    }

    private void SafeClose()
    {
        if (_isClosing) return;
        _isClosing = true;
        try { Close(); } catch { } // Swallow race condition errors
    }
}
```

## Common Pitfalls

1.  **File Locks**: Scanning folders while files are being written can crash. Use `try-catch` inside discovery loops.
2.  **UI Freezing**: **NEVER** run discovery or script execution on the UI thread. Use `Task.Run`.
3.  **DPI Awareness**: Ensure `app.manifest` enables per-monitor DPI awareness, otherwise the launcher may appear blurry or off-center on multi-monitor setups.
4.  **Admin Rights**: If part of the app needs Admin (e.g., modifying system hosts), the *entire* launcher might need to run as Admin, or it must launch a separate elevated process.

## Recommended Libraries
- **Gma.System.MouseKeyHook**: Global hotkeys.
- **HandyControl / MahApps.Metro**: Modern UI styles (Glass, Dark Mode).
- **CS-Script**: Runtime C# compilation.
- **GongSolutions.WPF.DragDrop**: Drag & drop support for adding items.
