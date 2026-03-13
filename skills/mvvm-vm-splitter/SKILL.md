---
name: "mvvm-vm-splitter"
description: "Deconstructs a Fat ViewModel into Use-Case driven Vertical Slices. Replaces UI-based splitting (Sidebar/Chat) with Behavior-based splitting (Commands/Queries). strictly forbids Messenger abuse."
---

# MVVM to VSA Deconstructor (Stop splitting by UI!)

## Purpose

Refactor a bloated, God-like ViewModel into strict **Vertical Slices based on Use Cases**, NOT UI regions.

**CRITICAL SHIFT**: Do NOT simply split `MainViewModel` into `SidebarViewModel` and `ChatViewModel`. That is UI-component thinking. VSA demands we extract **Behaviors** (e.g., `SelectFileFeature`, `SendMessageFeature`).

## When to Invoke

Invoke this skill when:
- A ViewModel has grown too large and contains multiple unrelated responsibilities.
- You need to extract logic out of a Fat ViewModel into independent CQRS Handlers.
- The code suffers from "Event Spaghetti" (abusing Messenger to send commands between VMs).

## Architecture Constraints & Paradigm Shift

```text
❌ WRONG (UI-Driven / Component Split):
Features/Main/
├── Sidebar/
│   └── SidebarViewModel.cs    (Contains Folder Logic)
├── Chat/
│   └── ChatViewModel.cs       (Contains Chat Logic)

✅ CORRECT (Use-Case Driven / VSA Split):
Features/Main/
├── MainViewModel.cs           (DUMB UI State + MediatR Dispatcher only)
├── UseCases/
│   ├── SelectFolder/
│   │   └── SelectFolderFeature.cs   (Command + Handler)
│   ├── SendChatMessage/
│   │   └── SendChatMessageFeature.cs (Command + Handler)
│   └── LoadHistory/
│       └── LoadHistoryFeature.cs    (Query + Handler)
└── State/
    └── IMainSessionState.cs   (Shared reactive state, if needed)

```

## Procedure (Strict VSA Compliance)

### 1. Identify Behaviors, NOT Components

Scan the Fat ViewModel and list the actual *actions* it performs (e.g., `SaveFile()`, `DeleteFolder()`, `LoadData()`). Each action becomes its own Slice.

### 2. Extract Logic into CQRS Features

For each action, create a complete Use Case file in `Features/<Module>/UseCases/<ActionName>/`:

```csharp
// Features/Main/UseCases/SelectFile/SelectFileFeature.cs
public static class SelectFileFeature
{
    public record Command(string FileId) : IRequest<Result>;
    public record Result(bool Success, FileData? Data);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IFileDataService _fileService;

        public Handler(IFileDataService fileService)
        {
            _fileService = fileService;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            // ALL core logic moves here. Zero UI dependencies.
            var data = await _fileService.LoadAsync(request.FileId);
            return new Result(true, data);
        }
    }
}

```

### 3. Neutering the ViewModel (Make it Dumb)

Strip all injected services (except `IMediator` and Shared State) from the ViewModel. Replace method bodies with simple Mediator dispatches.

```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    [ObservableProperty] private FileData? _currentFile;

    public MainViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [RelayCommand]
    private async Task SelectFileAsync(string fileId)
    {
        // NO LOGIC. Just Dispatch and Update State.
        var result = await _mediator.Send(new SelectFileFeature.Command(fileId));
        if (result.Success)
        {
            CurrentFile = result.Data;
        }
    }
}

```

### 4. Fix Cross-Component Communication (NO MESSENGER COMMANDS)

**CRITICAL RULE:** `WeakReferenceMessenger` MUST ONLY be used for **Domain Events** (Publishing that something *happened*), NEVER for **Commands** (Telling someone to *do* something).

**❌ AVOID (Event Spaghetti / Command via Messenger):**

```csharp
// WRONG: Sidebar telling Main to do something via Messenger
WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(id));

```

**✅ SOLUTION A: Shared Reactive State (Recommended for local module)**
If multiple dumb VMs (like a sidebar and a main view) need to share the selected file, inject a Shared State object.

```csharp
public class MainViewModel
{
    private readonly IWorkspaceState _state; // Singleton or Scoped state
    public MainViewModel(IWorkspaceState state) => _state = state;
}

```

**✅ SOLUTION B: Domain Events**
If a slice finishes an action and others need to know:

```csharp
// Inside the SelectFileFeature Handler:
await _mediator.Publish(new FileSelectedEvent(data));

// Any VM or Handler can implement INotificationHandler<FileSelectedEvent>

```

## Verification Checklist

* [ ] Has all business logic (`if/else`, service calls, DB access) been removed from the ViewModel?
* [ ] Are the extracted files named by **Use Case** (e.g., `RenameFileFeature.cs`) and not by UI component?
* [ ] Are all MediatR requests (`_mediator.Send`) strictly within the slice or dispatching to pure Handlers?
* [ ] **NO MESSENGER COMMANDS**: Ensure no `RequestXXXMessage` exists. Replace with Shared State or `IMediator.Send`.

## Non-Goals

* Do NOT extract logic into helper classes or "Services" (e.g., `FileService`). Extract them into CQRS Handlers!
* Do NOT create "Aggregator ViewModels" that instantiate child ViewModels in their constructors.
