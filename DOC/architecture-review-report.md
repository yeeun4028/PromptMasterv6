# PromptMaster v6 垂直切片架构评审报告

> **评审视角**：Jimmy Bogard（垂直切片架构创始人，MediatR 作者）
> **评审日期**：2026-03-09
> **评审范围**：整体架构层面

---

## 一、执行摘要

### 总体评级：🟡 **B+（良好，但有关键问题需修复）**

项目已成功采用垂直切片架构的核心思想，Features 文件夹按功能组织，Messages 迁移到位，Settings 已拆分为子 VM。但存在 **3 个 Critical 级别问题** 和 **5 个 Major 级别问题**，需要优先处理。

### 关键发现速览

| 级别 | 数量 | 核心问题 |
|------|------|----------|
| 🔴 Critical | 3 | Core 层污染、Infrastructure 反向依赖、DI 反模式 |
| 🟠 Major | 5 | SettingsVM 仍过大、根目录遗留、服务接口缺失等 |
| 🟡 Minor | 4 | 命名不一致、注释语言混合等 |

---

## 二、架构依赖关系图

### 2.1 当前依赖状态

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Features Layer                                  │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐   │
│  │  Main   │ │Settings │ │Launcher │ │External │ │Workspace│ │  PinTo  │   │
│  │         │ │         │ │         │ │ Tools   │ │         │ │ Screen  │   │
│  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘   │
│       │           │           │           │           │           │         │
│       └───────────┴───────────┴─────┬─────┴───────────┴───────────┘         │
│                                     │                                       │
│                                     ▼                                       │
│                          ┌─────────────────┐                                │
│                          │     Shared      │                                │
│                          │ (Messages+Svcs) │                                │
│                          └────────┬────────┘                                │
└───────────────────────────────────┼─────────────────────────────────────────┘
                                    │
            ┌───────────────────────┼───────────────────────┐
            │                       │                       │
            ▼                       ▼                       ▼
┌───────────────────┐    ┌───────────────────┐    ┌───────────────────┐
│    Core Layer     │◄───│ Infrastructure    │───►│   Features?!      │
│                   │    │     Layer         │    │  (反向依赖!)       │
│  Interfaces (7)   │    │                   │    │                   │
│  Models (14)      │    │  Services (21)    │    │  PinToScreen      │
│                   │    │  Converters       │    │  Main             │
│  ⚠️ 污染!         │    │  Helpers          │    │  Launcher         │
│  (依赖 Infra!)    │    │  Behaviors        │    │  ExternalTools    │
└───────────────────┘    └───────────────────┘    │  Settings         │
                                                  └───────────────────┘
```

### 2.2 依赖方向违规分析

| 源文件 | 违规依赖 | 问题级别 |
|--------|----------|----------|
| `Core/Models/AppConfig.cs` | → `Infrastructure/Converters` | 🔴 Critical |
| `Core/Models/ApiProfile.cs` | → `Infrastructure/Converters` | 🔴 Critical |
| `Infrastructure/Services/WindowManager.cs` | → 6 个 Features | 🔴 Critical |
| `Infrastructure/Services/DialogService.cs` | → `Features.ExternalTools.Dialogs` | 🔴 Critical |
| `Infrastructure/Services/GlobalShortcutCoordinator.cs` | → `Features.ExternalTools` | 🔴 Critical |

---

## 三、Critical 问题详解

### 🔴 C-1: Core 层被 Infrastructure 污染

**问题位置**：
- [Core/Models/AppConfig.cs:4](file:///e:/01_代码仓库/Github/PromptMasterv6/Core/Models/AppConfig.cs#L4)
- [Core/Models/ApiProfile.cs:3](file:///e:/01_代码仓库/Github/PromptMasterv6/Core/Models/ApiProfile.cs#L3)

**问题代码**：
```csharp
// Core/Models/AppConfig.cs
using PromptMasterv6.Infrastructure.Converters;  // ❌ Core 依赖 Infrastructure!

namespace PromptMasterv6.Core.Models
{
    public partial class AppConfig : ObservableObject
    {
        [ObservableProperty]
        [property: JsonConverter(typeof(JsonEncryptedStringConverter))]  // ❌ 使用 Infra 实现
        private string password = "";
    }
}
```

**Jimmy Bogard 视角**：
> "Core 层是架构的心脏。它应该对一切外部实现一无所知。当你的 Model 开始依赖 Infrastructure 的 Converter，你就已经破坏了依赖方向。这不是'小问题'——这是架构腐烂的开始。"

**修复方案**：

**方案 A（推荐）：将 Converter 移至 Core 层**
```
Core/
├── Converters/
│   └── JsonEncryptedStringConverter.cs  # 移动到这里
├── Interfaces/
└── Models/
```

**方案 B：使用依赖注入**
```csharp
// Core/Interfaces/IEncryptionService.cs
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

// Infrastructure/Services/EncryptionService.cs
public class EncryptionService : IEncryptionService { ... }

// Core/Models/AppConfig.cs - 不再直接使用 Converter
// 加密逻辑由 Service 层处理，Model 保持纯净
```

---

### 🔴 C-2: Infrastructure 层反向依赖 Features

**问题位置**：
- [Infrastructure/Services/WindowManager.cs:2-6](file:///e:/01_代码仓库/Github/PromptMasterv6/Infrastructure/Services/WindowManager.cs#L2-L6)

**问题代码**：
```csharp
// Infrastructure/Services/WindowManager.cs
using PromptMasterv6.Features.PinToScreen;      // ❌
using PromptMasterv6.Features.Main;              // ❌
using PromptMasterv6.Features.Launcher;          // ❌
using PromptMasterv6.Features.ExternalTools;     // ❌
using PromptMasterv6.Features.Settings;          // ❌

public class WindowManager : IWindowManager
{
    public void ShowLauncherWindow()
    {
        var vm = app.ServiceProvider.GetRequiredService<LauncherViewModel>();  // ❌
        var window = new LauncherWindow(vm);  // ❌ 直接 new Feature 的 Window
    }
}
```

**Jimmy Bogard 视角**：
> "这是典型的'上帝服务'反模式。WindowManager 试图知道所有窗口，这导致 Infrastructure 层必须引用所有 Features。在垂直切片架构中，每个 Feature 应该管理自己的窗口生命周期，而不是让一个中心化的服务来控制。"

**修复方案**：

**方案 A（推荐）：使用 Message 解耦**
```csharp
// Features/Launcher/Messages/ShowLauncherMessage.cs
public sealed record ShowLauncherMessage;

// Infrastructure/Services/WindowManager.cs - 只处理通用窗口操作
public class WindowManager : IWindowManager
{
    public WindowManager()
    {
        WeakReferenceMessenger.Default.Register<ShowLauncherMessage>(this, (_, _) =>
        {
            // 通过 DI 获取 Window，而不是直接引用类型
        });
    }
}

// 或者：每个 Feature 自己处理窗口显示
// Features/Launcher/LauncherViewModel.cs
public partial class LauncherViewModel
{
    [RelayCommand]
    private void Show() => WeakReferenceMessenger.Default.Send(new ShowLauncherMessage());
}
```

**方案 B：Window Registry 模式**
```csharp
// Core/Interfaces/IWindowRegistry.cs
public interface IWindowRegistry
{
    void Register<TViewModel, TWindow>() where TWindow : Window;
    Window? ResolveWindow(object viewModel);
}

// App.xaml.cs - 启动时注册
services.AddSingleton<IWindowRegistry, WindowRegistry>();
windowRegistry.Register<LauncherViewModel, LauncherWindow>();
windowRegistry.Register<SettingsViewModel, SettingsWindow>();
```

---

### 🔴 C-3: DI 反模式 - 直接 new 服务

**问题位置**：
- [Features/Settings/SettingsViewModel.cs:179](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Settings/SettingsViewModel.cs#L179)

**问题代码**：
```csharp
public SettingsViewModel(
    ISettingsService settingsService,
    // ... 其他注入
    BaiduService baiduService,      // ❌ 注入具体实现，非接口
    TencentService tencentService,  // ❌
    GoogleService googleService,    // ❌
    // ...
)
{
    _hotkeyService = new HotkeyService();  // ❌❌ 直接 new! 最严重的 DI 反模式
}
```

**Jimmy Bogard 视角**：
> "你在构造函数里 new 一个服务？这比不使用 DI 还糟糕——你既用了 DI，又绕过了它。这会让测试变得不可能，也会让生命周期管理失控。要么全部注入，要么全部 new，别混着来。"

**修复方案**：
```csharp
// 1. 为服务创建接口（如果不存在）
public interface IHotkeyService { ... }
public interface IBaiduService { ... }
public interface ITencentService { ... }
public interface IGoogleService { ... }

// 2. 在 App.xaml.cs 注册
services.AddSingleton<IHotkeyService, HotkeyService>();
services.AddSingleton<IBaiduService, BaiduService>();
services.AddSingleton<ITencentService, TencentService>();
services.AddSingleton<IGoogleService, GoogleService>();

// 3. 通过构造函数注入
public SettingsViewModel(
    IHotkeyService hotkeyService,
    IBaiduService baiduService,
    ITencentService tencentService,
    IGoogleService googleService)
{
    _hotkeyService = hotkeyService;  // ✅ 注入
    _baiduService = baiduService;    // ✅ 注入
    // ...
}
```

---

## 四、Major 问题详解

### 🟠 M-1: SettingsViewModel 仍然过大（1335 行）

**问题位置**：
- [Features/Settings/SettingsViewModel.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Settings/SettingsViewModel.cs)

**问题分析**：
虽然已拆分为 4 个子 VM（AiModelsVM、ApiProvidersVM、SyncVM、LauncherSettingsVM），但主 SettingsViewModel 仍然包含：
- 百度/腾讯/Google API 测试逻辑（约 300 行）
- LaunchBar 管理逻辑（约 50 行）
- 凭据加载/保存逻辑（约 200 行）

**Jimmy Bogard 视角**：
> "你拆分了子 VM，这很好。但为什么 API 测试逻辑还在主 VM 里？这些应该属于各自的 Feature 或子 VM。垂直切片的核心是'每个用例独立'——API 测试是一个用例，它不应该和设置 UI 混在一起。"

**修复建议**：
```
Features/Settings/
├── ApiCredentials/
│   └── ApiCredentialsViewModel.cs  # 移入百度/腾讯/Google 凭据逻辑
├── LaunchBar/
│   └── LaunchBarSettingsViewModel.cs  # 移入 LaunchBar 管理逻辑
├── SettingsViewModel.cs  # 只保留协调逻辑，目标 < 300 行
```

---

### 🟠 M-2: 根目录遗留 Converters 文件夹

**问题位置**：
- [Converters/](file:///e:/01_代码仓库/Github/PromptMasterv6/Converters)

**问题分析**：
根目录存在 9 个 Converter 文件，而 Infrastructure/Converters 已存在。这是重构遗留问题。

**修复方案**：
```
# 迁移到 Infrastructure/Converters
Converters/*.cs → Infrastructure/Converters/

# 或迁移到 Features/Shared/Converters（如果只被 Features 使用）
```

---

### 🟠 M-3: 服务缺少接口定义

**问题服务**：
| 服务 | 是否有接口 | 使用位置 |
|------|------------|----------|
| LoggerService | ❌ | 全局 |
| HotkeyService | ❌ | SettingsViewModel, GlobalShortcutCoordinator |
| GlobalKeyService | ❌ | GlobalShortcutCoordinator |
| FileDataService | ❌ | SettingsViewModel |
| BaiduService | ❌ | SettingsViewModel |
| TencentService | ❌ | SettingsViewModel |
| GoogleService | ❌ | SettingsViewModel |

**Jimmy Bogard 视角**：
> "没有接口的服务是测试的噩梦。你无法 mock 它们，无法验证调用，无法隔离测试。在垂直切片架构中，服务应该通过接口注入——这不是'过度工程'，这是基本的可测试性保障。"

---

### 🟠 M-4: Shared Services 接口定义位置不当

**问题位置**：
- [Features/Shared/Services/VariableService.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Shared/Services/VariableService.cs)
- [Features/Shared/Services/ContentConverterService.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Shared/Services/ContentConverterService.cs)
- [Features/Shared/Services/WebTargetService.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Shared/Services/WebTargetService.cs)

**问题分析**：
接口和实现在同一个文件中定义。虽然这在小型项目中常见，但违反了"接口与实现分离"原则。

**修复方案**：
```
Core/Interfaces/
├── IVariableService.cs          # 移动接口定义
├── IContentConverterService.cs
└── IWebTargetService.cs

Features/Shared/Services/
├── VariableService.cs           # 只保留实现
├── ContentConverterService.cs
└── WebTargetService.cs
```

---

### 🟠 M-5: GlobalUsings.cs 引用 Infrastructure 实现

**问题位置**：
- [GlobalUsings.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/GlobalUsings.cs)

**问题代码**：
```csharp
global using PromptMasterv6.Infrastructure.Services;  // ❌ 全局暴露实现层
```

**Jimmy Bogard 视角**：
> "GlobalUsings 是便利工具，但当你把 Infrastructure.Services 全局暴露，你就鼓励了开发者直接使用实现而非接口。这会悄悄破坏你的架构边界。"

**修复方案**：
```csharp
// GlobalUsings.cs - 只暴露 Core 层
global using PromptMasterv6.Core.Interfaces;
global using PromptMasterv6.Core.Models;

// 在需要 Infrastructure 实现的文件中单独 using
```

---

## 五、DI 注册策略评审

### 5.1 当前注册状态

| 服务类型 | 生命周期 | 评估 |
|----------|----------|------|
| ISettingsService | Singleton | ✅ 正确 |
| IAiService | Singleton | ✅ 正确 |
| IDataService | Singleton | ✅ 正确 |
| MainWindow | Singleton | ⚠️ 应为 Transient（如果支持多窗口） |
| MainViewModel | Transient | ✅ 正确 |
| SettingsViewModel | Transient | ✅ 正确 |
| AiModelsViewModel | Singleton | ⚠️ 子 VM 为 Singleton，父 VM 为 Transient，生命周期不匹配 |
| ApiProvidersViewModel | Singleton | ⚠️ 同上 |
| SyncViewModel | Singleton | ⚠️ 同上 |
| LauncherSettingsViewModel | Singleton | ⚠️ 同上 |

### 5.2 生命周期不匹配问题

**问题分析**：
```csharp
// App.xaml.cs
services.AddTransient<SettingsViewModel>();      // Transient
services.AddSingleton<AiModelsViewModel>();      // Singleton
services.AddSingleton<ApiProvidersViewModel>();  // Singleton
services.AddSingleton<SyncViewModel>();          // Singleton
services.AddSingleton<LauncherSettingsViewModel>(); // Singleton
```

**Jimmy Bogard 视角**：
> "父 VM 是 Transient，子 VM 是 Singleton——这会导致什么？每次创建新的 SettingsViewModel，它都会收到相同的子 VM 实例。如果子 VM 有状态，这些状态会在不同的 SettingsViewModel 实例间共享。这可能不是你想要的行为。"

**修复建议**：
```csharp
// 方案 A：全部 Transient（推荐）
services.AddTransient<SettingsViewModel>();
services.AddTransient<AiModelsViewModel>();
services.AddTransient<ApiProvidersViewModel>();
services.AddTransient<SyncViewModel>();
services.AddTransient<LauncherSettingsViewModel>();

// 方案 B：全部 Singleton（如果确实需要共享状态）
services.AddSingleton<SettingsViewModel>();
services.AddSingleton<AiModelsViewModel>();
// ...
```

---

## 六、架构改进路线图

### Phase 1: 紧急修复（1-2 天）

| 优先级 | 任务 | 预估工时 |
|--------|------|----------|
| P0 | 修复 Core 层污染（移动 Converter） | 2h |
| P0 | 修复 SettingsViewModel 中的 `new HotkeyService()` | 1h |
| P0 | 为 BaiduService/TencentService/GoogleService 创建接口 | 2h |

### Phase 2: 架构重构（3-5 天）

| 优先级 | 任务 | 预估工时 |
|--------|------|----------|
| P1 | 解耦 WindowManager 与 Features（使用 Message 或 Registry） | 4h |
| P1 | 迁移根目录 Converters | 1h |
| P1 | 统一 VM 生命周期策略 | 2h |
| P1 | 拆分 SettingsViewModel 剩余职责 | 4h |

### Phase 3: 完善优化（持续）

| 优先级 | 任务 | 预估工时 |
|--------|------|----------|
| P2 | 为所有服务创建接口 | 4h |
| P2 | 移动 Shared Services 接口到 Core | 1h |
| P2 | 移除 GlobalUsings 中的 Infrastructure 引用 | 1h |

---

## 七、评审结论

### 7.1 架构健康度评分

| 维度 | 得分 | 说明 |
|------|------|------|
| 切片边界完整性 | 7/10 | Features 组织良好，但存在跨层依赖 |
| 依赖方向正确性 | 5/10 | Core 和 Infrastructure 层有严重违规 |
| 服务归属合理性 | 8/10 | 大部分服务位置正确 |
| DI 使用规范性 | 6/10 | 存在直接 new 和注入实现类问题 |
| MVVM 规范遵循 | 9/10 | ObservableProperty/RelayCommand 使用正确 |

**综合得分：7.0/10**

### 7.2 Jimmy Bogard 最终评语

> "你的垂直切片架构已经初具形态——Features 按功能组织，Messages 解耦了 Feature 间通信，Settings 拆分展示了你对单一职责的理解。但你犯了一个经典错误：**你把分层架构的'分层思维'带进了垂直切片**。
>
> 垂直切片的核心不是'把代码放到不同的文件夹'，而是**让每个用例能够独立理解、独立修改、独立部署**。当你的 WindowManager 需要知道所有窗口类型，当你的 Core Model 需要引用 Infrastructure Converter，你就已经回到了分层架构的老路。
>
> 修复这些问题不需要重写——只需要重新审视边界。把 Converter 移到 Core，用 Message 解耦 WindowManager，为服务创建接口。这些改动不大，但会让你的架构真正'垂直'起来。"

---

## 附录 A：问题清单速查表

| ID | 级别 | 问题 | 位置 | 状态 |
|----|------|------|------|------|
| C-1 | 🔴 Critical | Core 层依赖 Infrastructure | Core/Models/*.cs | ✅ 已修复 |
| C-2 | 🔴 Critical | Infrastructure 反向依赖 Features | Infrastructure/Services/WindowManager.cs | ✅ 已修复 |
| C-3 | 🔴 Critical | DI 反模式 - 直接 new 服务 | Features/Settings/SettingsViewModel.cs:179 | ✅ 已修复 |
| M-1 | 🟠 Major | SettingsViewModel 仍然过大 | Features/Settings/SettingsViewModel.cs | ✅ 已修复 (拆分 ApiCredentialsVM) |
| M-2 | 🟠 Major | 根目录遗留 Converters | Converters/ | ✅ 已修复 |
| M-3 | 🟠 Major | 服务缺少接口定义 | Infrastructure/Services/*.cs | ✅ 已修复 |
| M-4 | 🟠 Major | Shared Services 接口位置不当 | Features/Shared/Services/*.cs | ✅ 已修复 |
| M-5 | 🟠 Major | GlobalUsings 暴露实现层 | GlobalUsings.cs | ✅ 已修复 |

---

*报告生成时间：2026-03-09*
*评审人：Jimmy Bogard 视角（AI 模拟）*
