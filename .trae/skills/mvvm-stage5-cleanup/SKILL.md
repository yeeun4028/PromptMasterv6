---
name: "mvvm-stage5-cleanup"
description: "Finalizes VSA refactoring by obliterating the Fat ViewModel, cleaning up Code-Behind dependencies, and enforcing strict Use-Case boundaries. Kills aggregator properties and cross-slice commands."
---

# MVVM Stage 5 Cleanup (Strict VSA Execution)

## Goal

This is the final verification and cleanup phase after extracting Use Cases and creating Dumb ViewModels. This skill acts as a strict architectural linter:

1. Obliterate Aggregator Properties (e.g., `ParentVM.ChildVM`).
2. Update Code-Behind to respect UI Composition or isolated ViewModels.
3. Eradicate ALL cross-slice commands/queries (`_mediator.Send` across boundaries).
4. Verify Domain Event subscriptions (`_mediator.Publish`).

## When to Invoke

Invoke when:
- You have finished extracting handlers/Use Cases for a feature.
- XAML bindings or Code-Behind still contain legacy dot-chains (e.g., `Binding SidebarVM.Folders`).
- You need to perform a final architectural audit on a module to ensure 100% VSA compliance.

## Procedure (The VSA Purge)

### 1. Destroy Aggregator Properties
Scan the parent ViewModel (e.g., `MainViewModel.cs`) and **delete** all properties that expose other ViewModels.

```csharp
// ❌ FATAL WRONG: Must be deleted!
public SidebarViewModel SidebarVM { get; }
public ChatViewModel ChatVM { get; }
public ObservableCollection<FolderItem> Folders => SidebarVM.Folders;

```

### 2. Fix Code-Behind References

If `MainWindow.xaml.cs` (or similar code-behind) was relying on the Aggregator, fix it by either resolving the specific Dumb ViewModel needed for that interaction, or relying on MediatR.

```csharp
// ❌ WRONG: Chaining through parent
var text = ViewModel.ChatVM.MiniInputText;

// ✅ CORRECT: Inject or resolve the specific isolated ViewModel, OR just dispatch a command
var text = _chatViewModel.MiniInputText;
// OR better yet, let XAML bindings handle state, and code-behind only handles pure UI events.

```

### 3. Eradicate Cross-Slice Commands (CRITICAL AUDIT)

Scan the entire module for `_mediator.Send(...)`.
**Rule:** A slice can ONLY send its OWN commands. It can NEVER send a command or query belonging to another slice/module.

```csharp
// ❌ FATAL WRONG: Cross-Module/Cross-Slice Command or Query
// Example: Main module trying to fetch from Settings module directly
var settings = await _mediator.Send(new GetSettingsQuery());
await _mediator.Send(new UpdateSidebarCommand());

// ✅ CORRECT: Use Shared State for reading, or Publish Events for writing
var settings = _configProvider.CurrentConfig; // Read shared state
await _mediator.Publish(new SidebarUpdateRequestedEvent()); // Fire domain event

```

### 4. Verify Shared State & Event Subscriptions

If slices need to communicate:

* Ensure they are implementing `INotificationHandler<T>` for MediatR events.
* Ensure any shared data (like the current user, or current folder) is pushed down into a Shared State service (e.g., `IWorkspaceState`) rather than passed around via Messenger.

### 5. Final XAML Binding Check

Ensure no XAML bindings are using dotted paths through ViewModels.

```xml
<!-- ❌ WRONG: Dot-chain through parent VM -->
<ListView ItemsSource="{Binding SidebarVM.Folders}" />

<!-- ✅ CORRECT: UI Composition or Direct Binding -->
<local:SidebarView />
<ListView ItemsSource="{Binding Folders}" />

```

## Output / Verification Checklist

* [ ] **NO Aggregators:** Parent ViewModel has ZERO references to child ViewModels.
* [ ] **NO Cross-Slice Send:** Zero instances of `_mediator.Send()` calling a contract outside its own Use Case folder.
* [ ] **Clean Code-Behind:** Code-behind references updated, removing all dot-chained ViewModel paths.
* [ ] **Build Succeeds:** Solution compiles successfully with strict boundaries.
