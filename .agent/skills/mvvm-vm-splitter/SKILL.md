---
name: "mvvm-vm-splitter"
description: "Splits a large WPF ViewModel into child VMs within the SAME feature module. Maintains vertical slice boundaries. Invoke when refactoring MVVM responsibilities within a feature."
---

# MVVM VM Splitter (Vertical Slice Edition)

## Purpose

Refactor a large ViewModel **within the same feature module** into smaller, single-responsibility child ViewModels.

**CRITICAL**: This skill respects **Vertical Feature Slice Architecture**:
- All new files MUST stay inside the same `Features/<ModuleName>/` folder
- NEVER create files in `Shared/` or root directories
- Cross-module communication via MediatR Messages or DI interfaces only

## When to Invoke

Invoke this skill when:
- A ViewModel within a feature module has too many unrelated responsibilities
- You need to split responsibilities **within the same feature boundary**
- You want to remove `new Service()` coupling while maintaining slice integrity

## Architecture Constraints

```
Features/
├── Main/                          # Feature Module
│   ├── Editor/                    # ✅ Child VM folder (SAME module)
│   │   └── EditorViewModel.cs
│   ├── Files/                     # ✅ Commands folder
│   ├── Messages/                  # ✅ Messages folder
│   ├── Queries/                   # ✅ Queries folder
│   └── MainViewModel.cs           # Parent VM
│
├── Settings/
│   ├── AiModels/                  # ✅ Child VM folder
│   │   └── AiModelsViewModel.cs
│   └── SettingsViewModel.cs
│
└── Shared/                        # ❌ NEVER put feature VMs here!
```

## Procedure (Vertical Slice Compliant)

### 1. Identify the Feature Module
- Confirm the target ViewModel belongs to a specific `Features/<Module>/`
- All new files will be created **inside this module only**

### 2. Create Child ViewModel(s) Within Module
```
Features/Main/
├── Sidebar/
│   └── SidebarViewModel.cs    # ✅ New child VM
├── Chat/
│   └── ChatViewModel.cs       # ✅ New child VM
└── MainViewModel.cs           # Parent aggregates children
```

### 3. Wire DI (Module-Scoped)
```csharp
// In App.xaml.cs or Module DI registration
services.AddSingleton<MainViewModel>();
services.AddSingleton<SidebarViewModel>();
services.AddSingleton<ChatViewModel>();
```

### 4. Update XAML Bindings (Same Module)
```xml
<!-- MainWindow.xaml in Features/Main/ -->
<TextBox Text="{Binding SidebarVM.SearchText}" />
<ListView ItemsSource="{Binding ChatVM.Messages}" />
```

### 5. Cross-Module Communication
**NEVER** reference ViewModels from other modules directly.

**DO** use:
- `MediatR` requests/commands for cross-module calls
- `WeakReferenceMessenger` for loose coupling
- Shared interfaces in `Features/Shared/Models/`

```csharp
// ✅ Correct: Message-based communication
public record RequestSelectFileMessage(Guid FileId);

// In SidebarViewModel (Features/Main/Sidebar/)
WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(id));

// In MainViewModel (Features/Main/)
WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(...);
```

### 6. Verify
- Build succeeds
- No cross-module direct references
- All new files are inside `Features/<Module>/`

## File Placement Rules

| File Type | Location | Example |
|-----------|----------|---------|
| Child ViewModel | `Features/<Module>/<SubFolder>/` | `Features/Main/Sidebar/SidebarViewModel.cs` |
| Commands | `Features/<Module>/Commands/` | `Features/Main/Files/ChangeFileIconCommand.cs` |
| Queries | `Features/<Module>/Queries/` | `Features/Main/Queries/LoadAppDataQuery.cs` |
| Messages | `Features/<Module>/Messages/` | `Features/Main/Messages/FolderSelectionChangedMessage.cs` |
| Shared Models | `Features/Shared/Models/` | `Features/Shared/Models/AppConfig.cs` |

## Anti-Patterns (AVOID)

```csharp
// ❌ WRONG: Cross-module direct reference
public class SidebarViewModel
{
    private readonly SettingsViewModel _settings; // Different module!
}

// ✅ CORRECT: Use MediatR or shared interface
public class SidebarViewModel
{
    private readonly IMediator _mediator;
    // Or inject IConfigService from Shared/Models/
}
```

## Non-Goals

- Do NOT create files outside the feature module
- Do NOT introduce cross-module ViewModel dependencies
- Do NOT break the vertical slice boundary
