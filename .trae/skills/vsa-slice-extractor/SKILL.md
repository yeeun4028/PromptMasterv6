---
name: "vsa-slice-extractor"
description: "Extracts a TRUE vertical slice from monolithic WPF code. Enforces Use-Case driven organization, extremely thin ViewModels, and event-driven cross-slice communication."
---

# VSA Slice Extractor (Strict Vertical Slice Architecture)

## Purpose

Extract a feature slice from monolithic WPF code through a **Strict VSA Refactoring Process**:
1. **Use-Case Driven Organization** - Organize folders by actions/verbs, NOT by entities.
2. **Dumb UI & ViewModels** - Strip ALL business logic from ViewModels. They are just empty shells for XAML bindings and command dispatching.
3. **Pure CQRS Handlers** - Sink all actual logic into pure Feature Handlers.
4. **Event-Driven Isolation** - Cross-slice communication strictly via Domain Events (Publish), NEVER cross-slice Commands (Send).

## When to Invoke

Invoke when:
- A large ViewModel or View needs to be broken down.
- You are extracting logic into independent, highly cohesive Vertical Slices.
- You need to eliminate "UI-driven" or "Entity-driven" folder structures in favor of "Behavior-driven" structures.

## Architecture Overview (The VSA Way)

**CRITICAL RULE:** Do NOT group by Noun/Entity (e.g., `AiModels/`). Group by Use Case!

```text
Before (Monolithic/Fat MVVM):
Features/Settings/
├── SettingsView.xaml
└── SettingsViewModel.cs       (Contains logic for TestConnection, DeleteModel, etc.)

After (Strict Vertical Slice):
Features/Settings/AiModels/
├── AiModelsView.xaml          (Dumb Presentation)
├── AiModelsViewModel.cs       (Dumb State Shell + Dispatcher)
│
├── UseCases/                  ← THIS IS THE CORE OF VSA
│   ├── TestConnection/
│   │   └── TestConnectionFeature.cs  (Command, Result, Handler)
│   ├── DeleteModel/
│   │   └── DeleteModelFeature.cs     (Command, Result, Handler)
│   └── UpdateConfig/
│       └── UpdateConfigFeature.cs    (Command, Result, Handler)
│
└── Events/
    └── AiModelDeletedEvent.cs (Domain Event for cross-slice)

```

## Step-by-Step Procedure

### Step 1: Create the Dumb Presentation Layer

**Goal**: The ViewModel must contain ZERO business logic. It only holds state and forwards actions.

**{SliceGroup}ViewModel.cs**:

```csharp
public partial class {SliceGroup}ViewModel : ObservableObject
{
    private readonly IMediator _mediator; // ONLY inject Mediator or specific Handlers. NO domain services!

    // Pure UI State
    [ObservableProperty] private string _statusText;

    public {SliceGroup}ViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        // NO LOGIC HERE. Just dispatch!
        var command = new TestConnectionFeature.Command(CurrentConfig);
        var result = await _mediator.Send(command); // Local slice execution

        StatusText = result.Message; // Update dumb state
    }
}

```

### Step 2: Extract Pure CQRS Use Cases

**Goal**: Every action the user can take is an isolated class containing its own Input (Command), Output (Result), and Logic (Handler).

**UseCases/{Action}/{Action}Feature.cs**:

```csharp
using MediatR;
// NO UI namespaces allowed here!

namespace PromptMasterv6.Features.Settings.AiModels.UseCases.TestConnection
{
    public static class TestConnectionFeature
    {
        // 1. Input Contract
        public record Command(string ApiKey, string Endpoint) : IRequest<Result>;

        // 2. Output Contract
        public record Result(bool Success, string Message);

        // 3. Pure Business Logic Handler
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly IHttpClientFactory _httpClientFactory;

            // Inject infrastructure directly into the handler
            public Handler(IHttpClientFactory httpClientFactory)
            {
                _httpClientFactory = httpClientFactory;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                // ALL Business logic lives here.
                // Highly cohesive. If "Test Connection" changes, ONLY this file changes.
                return new Result(true, "Connection Successful");
            }
        }
    }
}

```

### Step 3: Event-Driven Cross-Slice Communication

**Goal**: Slices must NOT know about each other. They communicate by announcing things happened (Events).

**Events/{Entity}{PastTenseVerb}Event.cs**:

```csharp
namespace PromptMasterv6.Features.Settings.AiModels.Events
{
    // Naming Rule: Past tense! It already happened.
    public record AiModelDeletedEvent(string ModelId) : INotification;
}

```

**Inside the Handler (Publishing):**

```csharp
public async Task<Result> Handle(Command request, CancellationToken ct)
{
    _database.Delete(request.Id);

    // Fire and forget. We don't care who listens.
    await _mediator.Publish(new AiModelDeletedEvent(request.Id), ct);

    return new Result(true);
}

```

## Verification Checklist (AI MUST VERIFY)

* [ ] **Are Folders Action-Oriented?** Check if directories inside the feature are named after Use Cases (e.g., `TestConnection/`) rather than abstract concepts.
* [ ] **Is ViewModel Brainless?** The ViewModel must NOT contain `if/else` business rules, API calls, or database queries.
* [ ] **No Cross-Slice Commands?** Code must NEVER use `_mediator.Send(new CommandFromAnotherSlice())`. It must only `Publish` events.
* [ ] **Dependency Injection**: Are the new Handlers registered in DI? (Or does MediatR auto-discover them?)

## Anti-Patterns to Destroy

```csharp
// ❌ FATAL WRONG: Logic in ViewModel
public async Task Delete() {
    _db.Remove(item); // UI layer talking to DB!
    Status = "Deleted";
}

// ❌ FATAL WRONG: Cross-slice command (Coupling!)
public async Task UpdateSync() {
    // Calling Settings slice from Main slice! NO!
    await _mediator.Send(new Features.Settings.Sync.UpdateSyncCommand());
}

// ✅ CORRECT: Publish Event
public async Task UpdateSync() {
    await _mediator.Publish(new SyncTriggeredEvent()); // Let the Sync slice listen and handle it itself.
}

```
