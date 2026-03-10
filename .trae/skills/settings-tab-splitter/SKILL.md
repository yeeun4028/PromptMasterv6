---
name: "settings-tab-splitter"
description: "Splits a Settings tab into separate ViewModel and View files within Features/Settings/. Maintains vertical slice boundaries with DI registration. Invoke when refactoring SettingsView tabs into child VMs."
---

# Settings Tab Splitter (Vertical Slice Edition)

## Purpose

Split a Settings tab page into its own ViewModel and View files **within the `Features/Settings/` module**, following Vertical Feature Slice Architecture.

## When to Invoke

Invoke when:
- A Settings tab has grown too complex and needs its own ViewModel
- You want to separate concerns within the Settings module
- Refactoring `SettingsView.xaml` tabs into independent child components

## Architecture Context

```
Features/Settings/
├── AiModels/
│   └── AiModelsViewModel.cs      # ✅ Child VM for AI Models tab
├── ApiCredentials/
│   └── ApiCredentialsViewModel.cs # ✅ Child VM for API tab
├── Launcher/
│   └── LauncherSettingsViewModel.cs
├── Sync/
│   └── SyncViewModel.cs
├── SettingsContainerViewModel.cs  # Parent VM (aggregator)
├── SettingsView.xaml              # Main view with tab content hosts
├── SettingsView.xaml.cs
├── SettingsViewModel.cs           # Legacy or wrapper
└── SettingsWindow.xaml
```

## Procedure

### 1. Identify Target Tab
- Determine which tab needs splitting (e.g., "AI Models", "API Credentials")
- Confirm it's within `Features/Settings/` module

### 2. Create Child ViewModel
Create in `Features/Settings/<TabName>/`:

```csharp
// Features/Settings/AiModels/AiModelsViewModel.cs
namespace PromptMasterv6.Features.Settings.AiModels;

public partial class AiModelsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    
    [ObservableProperty] private ObservableCollection<AiModelConfig> _models;
    [ObservableProperty] private AiModelConfig? _selectedModel;
    
    public AiModelsViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [RelayCommand]
    private async Task LoadModels()
    {
        // Load logic
    }
}
```

### 3. Update Parent ViewModel (Aggregator)
```csharp
// Features/Settings/SettingsContainerViewModel.cs
public partial class SettingsContainerViewModel : ObservableObject
{
    public AiModelsViewModel AiModelsVM { get; }
    public ApiCredentialsViewModel ApiCredentialsVM { get; }
    public LauncherSettingsViewModel LauncherVM { get; }
    public SyncViewModel SyncVM { get; }
    
    public SettingsContainerViewModel(
        AiModelsViewModel aiModelsVM,
        ApiCredentialsViewModel apiCredentialsVM,
        LauncherSettingsViewModel launcherVM,
        SyncViewModel syncVM)
    {
        AiModelsVM = aiModelsVM;
        ApiCredentialsVM = apiCredentialsVM;
        LauncherVM = launcherVM;
        SyncVM = syncVM;
    }
}
```

### 4. Register in DI
```csharp
// In App.xaml.cs or DI registration
services.AddSingleton<AiModelsViewModel>();
services.AddSingleton<ApiCredentialsViewModel>();
services.AddSingleton<LauncherSettingsViewModel>();
services.AddSingleton<SyncViewModel>();
services.AddSingleton<SettingsContainerViewModel>();
```

### 5. Update XAML Bindings
```xml
<!-- Features/Settings/SettingsView.xaml -->
<TabControl SelectedIndex="{Binding SelectedTabIndex}">
    <TabItem Header="AI Models">
        <ContentControl Content="{Binding AiModelsVM}" />
        <!-- Or inline bindings: -->
        <ListView ItemsSource="{Binding AiModelsVM.Models}" />
    </TabItem>
    
    <TabItem Header="API Credentials">
        <ContentControl Content="{Binding ApiCredentialsVM}" />
    </TabItem>
</TabControl>
```

### 6. Create Dedicated View (Optional)
For complex tabs, create a separate View:

```
Features/Settings/AiModels/
├── AiModelsViewModel.cs
└── AiModelsView.xaml        # Optional dedicated view
```

```xml
<!-- Features/Settings/AiModels/AiModelsView.xaml -->
<UserControl x:Class="PromptMasterv6.Features.Settings.AiModels.AiModelsView"
             DataContext="{Binding AiModelsVM, RelativeSource={RelativeSource AncestorType=SettingsView}}">
    <!-- Tab content -->
</UserControl>
```

## File Placement Rules

| File Type | Location |
|-----------|----------|
| Child ViewModel | `Features/Settings/<TabName>/<Name>ViewModel.cs` |
| Child View (optional) | `Features/Settings/<TabName>/<Name>View.xaml` |
| Messages | `Features/Settings/Messages/` |
| Shared Models | `Features/Shared/Models/` |

## Anti-Patterns (AVOID)

```csharp
// ❌ WRONG: Creating outside Settings module
Features/Shared/ViewModels/AiModelsViewModel.cs

// ❌ WRONG: Cross-module direct reference
public class AiModelsViewModel
{
    private readonly MainViewModel _main; // Different module!
}

// ✅ CORRECT: Use shared services/interfaces
public class AiModelsViewModel
{
    private readonly IConfigService _config; // From Features/Shared/Models/
}
```

## Verification

- ✅ All new files inside `Features/Settings/<TabName>/`
- ✅ DI registration complete
- ✅ XAML bindings updated
- ✅ Build succeeds
- ✅ Tab functionality preserved
