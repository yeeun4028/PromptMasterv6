---
name: "vsa-slice-extractor"
description: "Extracts a vertical slice from monolithic WPF code: UI isolation, CQRS handlers, and event-driven communication. Invoke when refactoring a large Settings tab or ViewModel into independent slice."
---

# VSA Slice Extractor (Vertical Slice Architecture)

## Purpose

Extract a feature slice from monolithic WPF code through a **three-step refactoring process**:
1. **UI Physical Isolation** - Separate View/XAML
2. **CQRS Use Case Sinking** - Extract business logic to Handlers
3. **Event-Driven Communication** - Decouple cross-slice dependencies

## When to Invoke

Invoke when:
- A Settings tab has grown too large (500+ lines XAML or 300+ lines C#)
- Business logic is tightly coupled in ViewModel
- Cross-feature communication uses direct method calls
- You want to apply Vertical Slice Architecture principles

## Architecture Overview

```
Before (Monolithic):
Features/Settings/
├── SettingsView.xaml          (2000+ lines, all tabs mixed)
└── SettingsViewModel.cs       (500+ lines, all logic mixed)

After (Vertical Slice):
Features/Settings/
├── SettingsView.xaml          (Container only)
├── SettingsViewModel.cs       (Coordinator, event listener)
│
└── AiModels/                  ← Independent Vertical Slice
    ├── AiModelsView.xaml      (UI)
    ├── AiModelsView.xaml.cs
    ├── AiModelsViewModel.cs   (Thin, UI state only)
    ├── TestAiConnectionFeature.cs   (CQRS Use Case)
    ├── DeleteAiModelFeature.cs      (CQRS Use Case)
    └── Messages/
        └── AiModelDeletedMessage.cs (Domain Event)
```

## Three-Step Procedure

### Step 1: UI Physical Isolation

**Goal**: Extract UI into independent View file.

#### 1.1 Create Slice Directory Structure
```
Features/Settings/{SliceName}/
├── {SliceName}View.xaml
├── {SliceName}View.xaml.cs
├── {SliceName}ViewModel.cs
├── Messages/
│   └── (events will be added in Step 3)
└── (Feature files will be added in Step 2)
```

#### 1.2 Create View Files

**{SliceName}View.xaml**:
```xml
<UserControl x:Class="PromptMasterv6.Features.Settings.{SliceName}.{SliceName}View"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             mc:Ignorable="d" d:DesignWidth="700" d:DesignHeight="500">
    <StackPanel>
        <!-- Extracted content from SettingsView.xaml -->
    </StackPanel>
</UserControl>
```

**{SliceName}View.xaml.cs**:
```csharp
using System.Windows.Controls;

namespace PromptMasterv6.Features.Settings.{SliceName}
{
    public partial class {SliceName}View : UserControl
    {
        public {SliceName}View()
        {
            InitializeComponent();
        }
    }
}
```

#### 1.3 Update Parent View

**SettingsView.xaml** - Add namespace and replace tab content:
```xml
<!-- Add namespace -->
xmlns:{sliceName}="clr-namespace:PromptMasterv6.Features.Settings.{SliceName}"

<!-- Replace Tab Content -->
<StackPanel>
    <StackPanel.Style>...</StackPanel.Style>
    <{sliceName}:{SliceName}View DataContext="{Binding {SliceName}VM}" />
</StackPanel>
```

#### 1.4 Move Shared Styles to App.xaml

If View uses styles defined in parent (e.g., `CardStyle`, `ModernComboBoxStyle`), move them to `App.xaml`:
```xml
<!-- App.xaml -->
<Application.Resources>
    <Style x:Key="CardStyle" TargetType="Border">...</Style>
    <Style x:Key="ModernComboBoxStyle" TargetType="ComboBox">...</Style>
</Application.Resources>
```

---

### Step 2: CQRS Use Case Sinking

**Goal**: Extract business logic from ViewModel into pure Handler classes.

#### 2.1 Create Feature Files

**{Action}Feature.cs** (one per use case):
```csharp
using PromptMasterv6.Infrastructure.Services;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.{SliceName}
{
    public static class {Action}Feature
    {
        // Input Contract
        public record Command(string Param1, string Param2);

        // Output Contract
        public record Result(bool Success, string Message);

        // Pure Business Logic Handler
        public class Handler
        {
            private readonly SomeService _service;

            public Handler(SomeService service)
            {
                _service = service;
            }

            public async Task<Result> Handle(Command request)
            {
                // Business logic here - NO UI dependencies
                return new Result(true, "Success");
            }
        }
    }
}
```

#### 2.2 Update ViewModel to Use Handlers

**Before**:
```csharp
public partial class {SliceName}ViewModel : ObservableObject
{
    private readonly AiService _aiService;  // Direct dependency

    [RelayCommand]
    private async Task TestConnection()
    {
        var result = await _aiService.TestConnectionAsync(...);  // Direct call
        // Update UI state
    }
}
```

**After**:
```csharp
public partial class {SliceName}ViewModel : ObservableObject
{
    private readonly TestConnectionFeature.Handler _testHandler;  // Handler only

    public {SliceName}ViewModel(
        TestConnectionFeature.Handler testHandler,
        DeleteItemFeature.Handler deleteHandler)
    {
        _testHandler = testHandler;
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        var cmd = new TestConnectionFeature.Command(...);
        var result = await _testHandler.Handle(cmd);  // Through handler
        // Update UI state
    }
}
```

#### 2.3 Register Handlers in DI

**App.xaml.cs**:
```csharp
services.AddSingleton<TestConnectionFeature.Handler>();
services.AddSingleton<DeleteItemFeature.Handler>();
services.AddSingleton<{SliceName}ViewModel>();
```

---

### Step 3: Event-Driven Communication

**Goal**: Decouple cross-slice communication via domain events.

#### 3.1 Define Domain Event

**Messages/{Event}Message.cs**:
```csharp
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.{SliceName}.Messages
{
    // Event naming: Past tense (something happened)
    public class {Entity}DeletedMessage
    {
        public string DeletedId { get; }
        public string DeletedName { get; }

        public {Entity}DeletedMessage(EntityConfig deleted)
        {
            DeletedId = deleted.Id;
            DeletedName = deleted.Name;
        }
    }
}
```

#### 3.2 Publish Event from Handler

**{Action}Feature.cs**:
```csharp
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Features.Settings.{SliceName}.Messages;

public class Handler
{
    public void Handle(Command request)
    {
        // 1. Execute core logic
        // 2. Persist changes
        _service.Save();

        // 3. Broadcast domain event (Fire and Forget)
        WeakReferenceMessenger.Default.Send(new {Entity}DeletedMessage(deletedItem));
    }
}
```

#### 3.3 Subscribe to Event in Other Slices

**OtherViewModel.cs**:
```csharp
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Features.Settings.{SliceName}.Messages;

public partial class OtherViewModel : ObservableObject, 
    IRecipient<{Entity}DeletedMessage>
{
    public OtherViewModel()
    {
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive({Entity}DeletedMessage message)
    {
        // React to the event
        // e.g., clean up related data, show notification
    }
}
```

---

## File Placement Rules

| File Type | Location |
|-----------|----------|
| View XAML | `Features/{Module}/{SliceName}/{SliceName}View.xaml` |
| View Code-Behind | `Features/{Module}/{SliceName}/{SliceName}View.xaml.cs` |
| ViewModel | `Features/{Module}/{SliceName}/{SliceName}ViewModel.cs` |
| Feature/Handler | `Features/{Module}/{SliceName}/{Action}Feature.cs` |
| Domain Events | `Features/{Module}/{SliceName}/Messages/{Event}Message.cs` |
| Shared Styles | `App.xaml` (global) |

## Verification Checklist

- [ ] **Step 1 Complete**: View extracted, parent XAML references new View
- [ ] **Step 2 Complete**: Handlers created, ViewModel uses handlers
- [ ] **Step 3 Complete**: Events defined, cross-slice communication via messages
- [ ] DI registration updated
- [ ] Build succeeds
- [ ] No direct cross-slice ViewModel references
- [ ] Shared styles moved to App.xaml

## Anti-Patterns (AVOID)

```csharp
// ❌ WRONG: Direct cross-slice call
public void DeleteItem()
{
    _otherViewModel.Refresh();  // Tight coupling!
}

// ❌ WRONG: Handler with UI dependencies
public class Handler
{
    public void Handle()
    {
        _dialogService.ShowAlert(...);  // UI in business logic!
    }
}

// ✅ CORRECT: Event-driven communication
public void Handle()
{
    WeakReferenceMessenger.Default.Send(new ItemDeletedMessage(item));
}
// Other slice listens and decides what to do
```

## Example: AI Models Slice

See the completed refactoring:
- `Features/Settings/AiModels/AiModelsView.xaml`
- `Features/Settings/AiModels/AiModelsViewModel.cs`
- `Features/Settings/AiModels/TestAiConnectionFeature.cs`
- `Features/Settings/AiModels/DeleteAiModelFeature.cs`
- `Features/Settings/AiModels/Messages/AiModelDeletedMessage.cs`
