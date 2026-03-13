---
name: "settings-tab-splitter"
description: "Extracts a Settings tab into a fully isolated Vertical Slice. Strictly enforces UI Composition and forbids Aggregator ViewModels (Constructor Over-injection)."
---

# Settings Tab Splitter (Strict VSA Edition)

## Purpose

Extract a complex Settings tab into its own independent Vertical Slice.
**CRITICAL SHIFT**: We must destroy the "Aggregator ViewModel" anti-pattern. The Parent Settings ViewModel MUST NOT inject or strongly reference the Child Tab ViewModels. We use **UI Composition** to keep slices 100% isolated.

## When to Invoke

Invoke when:
- A single tab in the Settings window has grown too complex.
- You need to add a new Settings tab without modifying the constructor of the parent `SettingsViewModel` (Open-Closed Principle).
- You are breaking down a monolithic Settings module into independent feature slices.

## Architecture Context & Paradigm Shift

```text
❌ WRONG (The Aggregator Anti-Pattern):
Features/Settings/SettingsContainerViewModel.cs
// Bad: Parent knows about all children. Tight coupling!
public SettingsContainerViewModel(AiModelsVM ai, ApiVM api, SyncVM sync) { ... }

✅ CORRECT (Strict VSA / UI Composition):
Features/Settings/
├── SettingsView.xaml              (The Host/Shell - holds the TabControl)
├── SettingsViewModel.cs           (Only handles Shell logic, like "Close Window")
│
├── AiModels/                      ← INDEPENDENT SLICE
│   ├── AiModelsView.xaml          (Resolves its own VM)
│   ├── AiModelsViewModel.cs       (Pure UI state)
│   └── UseCases/                  (CQRS Handlers)
│
└── ApiCredentials/                ← INDEPENDENT SLICE
    ├── ApiCredentialsView.xaml
    └── ...

```

## Procedure (Strict VSA Compliance)

### 1. Create the Isolated Slice (View + ViewModel)

Create the new Tab's View and ViewModel in its own folder.

```csharp
// Features/Settings/AiModels/AiModelsViewModel.cs
public partial class AiModelsViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    // Pure State. No Business Logic!
    [ObservableProperty] private ObservableCollection<AiModelConfig> _models;

    public AiModelsViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }
}

```

### 2. UI Composition (The Smart Way)

The parent `SettingsView.xaml` should simply host the Child Views. The Parent ViewModel (`SettingsViewModel`) is bypassed entirely for tab content.

**Features/Settings/SettingsView.xaml**:

```xml
<TabControl>
    <TabItem Header="AI Models">
        <aimodels:AiModelsView />
    </TabItem>

    <TabItem Header="API Credentials">
        <apicreds:ApiCredentialsView />
    </TabItem>
</TabControl>

```

*(Note for AI: Instruct the View's code-behind to resolve its ViewModel via DI, or use a MarkupExtension/Locator, so the parent VM doesn't have to instantiate it).*

### 3. Extract Logic to Handlers

Ensure any `Save()`, `Load()`, or `TestConnection()` methods in the new Tab are extracted into MediatR CQRS Handlers within the slice's `UseCases/` folder (as per `vsa-slice-extractor` rules).

### 4. Register in DI

```csharp
// In App.xaml.cs or Dependency Injection Registry
services.AddTransient<AiModelsViewModel>(); // Transient is often safer for tabs if state isn't globally shared
services.AddTransient<AiModelsView>();

```

## Anti-Patterns (AI MUST AVOID)

```csharp
// ❌ FATAL WRONG: Constructor Over-injection in Parent
public class SettingsViewModel
{
    public AiModelsViewModel AiModels { get; }

    // Breaking OCP! Every new tab requires changing this constructor!
    public SettingsViewModel(AiModelsViewModel aiModels, ApiCredentialsViewModel api)
    {
        AiModels = aiModels;
    }
}

// ❌ FATAL WRONG: Cross-Slice Dependencies
public class AiModelsViewModel
{
    // A slice cannot inject another slice's ViewModel!
    public AiModelsViewModel(ApiCredentialsViewModel apiVm) { }
}

```

## Verification Checklist

* [ ] Is the new Tab a complete, self-contained Vertical Slice (View, ViewModel, UseCases)?
* [ ] Have you **removed** the child ViewModel from the Parent ViewModel's constructor?
* [ ] Does the Parent XAML use UI Composition (e.g., `<local:MyTabView />`) instead of data-binding to properties on the Parent VM?
* [ ] Is the new ViewModel registered in the DI container?
