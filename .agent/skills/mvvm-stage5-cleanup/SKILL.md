---
name: "mvvm-stage5-cleanup"
description: "Fixes code-behind references after MVVM split within a feature module, removes compatibility shims, and verifies messenger registrations. Invoke after XAML bindings are moved to child VMs."
---

# MVVM Stage 5 Cleanup (Vertical Slice Edition)

## Goal

After updating XAML bindings to child ViewModels **within the same feature module**, this skill:

1. Updates code-behind references to use child VM properties
2. Removes compatibility forwarding properties from parent ViewModel
3. Verifies `WeakReferenceMessenger` registrations
4. Ensures build succeeds and vertical slice boundaries are maintained

## When to Invoke

Invoke when:
- A feature module's ViewModels have been split into child VMs
- XAML bindings point to child VMs (e.g., `SidebarVM.*`, `ChatVM.*`)
- You want to remove temporary adapter properties from parent ViewModel
- Code-behind still references old parent VM properties

## Architecture Context

```
Features/Main/
├── Sidebar/
│   └── SidebarViewModel.cs     # Child VM owns: Folders, SelectedFolder
├── Chat/
│   └── ChatViewModel.cs        # Child VM owns: MiniInputText, Messages
├── Messages/
│   └── *.cs                    # Module-internal messages
├── MainWindow.xaml             # View
├── MainWindow.xaml.cs          # Code-behind
└── MainViewModel.cs            # Parent VM (aggregator)
```

## Procedure

### 1. Scan Code-Behind (Same Module Only)
Find and update references in `MainWindow.xaml.cs`:

```csharp
// ❌ OLD: Direct parent VM reference
ViewModel.MiniInputText
ViewModel.Folders

// ✅ NEW: Child VM reference
ViewModel.ChatVM.MiniInputText
ViewModel.SidebarVM.Folders
```

### 2. Responsibility Mapping

| Responsibility | Child VM | Properties |
|----------------|----------|------------|
| File/Folder Management | `SidebarViewModel` | `Folders`, `SelectedFolder`, `DragDropHandler` |
| Chat/Input | `ChatViewModel` | `MiniInputText`, `Messages`, `IsAiProcessing` |
| Content Editing | `EditorViewModel` | `CurrentFile`, `Content`, `Variables` |
| Global State | `MainViewModel` | `Config`, `IsFullMode`, `AppData` |

### 3. Clean Parent ViewModel
Remove forwarding properties that were only for XAML compatibility:

```csharp
// ❌ REMOVE these from MainViewModel
public ObservableCollection<FolderItem> Folders => SidebarVM.Folders;
public string MiniInputText { get => ChatVM.MiniInputText; set => ChatVM.MiniInputText = value; }

// ✅ KEEP global state in MainViewModel
public AppConfig Config { get; }
public bool IsFullMode { get; set; }
```

### 4. Verify Messenger Registrations
Ensure messages are registered correctly:

```csharp
// In MainViewModel constructor
WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, OnFolderChanged);
WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, OnSelectFileRequested);

// In child ViewModels - send messages, don't reference parent
WeakReferenceMessenger.Default.Send(new FolderSelectionChangedMessage(folderId));
```

### 5. Validate Vertical Slice Integrity
- ✅ All code-behind references updated
- ✅ No cross-module direct references
- ✅ Compatibility shims removed
- ✅ Build succeeds
- ✅ Startup smoke test passes

## Cross-Module Communication Check

If code-behind needs data from another module:

```csharp
// ❌ WRONG: Direct reference to other module's VM
ViewModel.SettingsVM.SomeProperty

// ✅ CORRECT: Use MediatR or shared state
await _mediator.Send(new GetSettingsQuery());
// Or inject shared service from Features/Shared/Models/
```

## Output

- Compiling solution with clean code-behind
- No compatibility shims in parent ViewModel
- Messenger subscriptions verified
- Vertical slice boundaries preserved
