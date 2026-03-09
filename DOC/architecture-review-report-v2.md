# PromptMaster v6 垂直切片架构深度评审报告

> **评审视角**：Jimmy Bogard（垂直切片架构创始人，MediatR/Carter 作者）
> **评审日期**：2026-03-09
> **评审范围**：整体架构层面（二度评审）
> **评审风格**：专业深度 + 犀利视角

---

## 零、开篇语：Jimmy Bogard 的审视

> "我看过太多项目声称'我们用了垂直切片架构'。但当你打开代码，发现他们只是把文件夹重命名了一下。垂直切片不是文件夹组织——它是**让每个用例能够独立理解、独立修改、独立测试**的架构哲学。
>
> 今天，我要用最严苛的眼光审视这个项目。不是因为我刻薄，而是因为**架构债务比技术债务更致命**——技术债务你可以重构，架构债务意味着你要重写。"

---

## 一、架构健康度总评

### 1.1 评分矩阵

| 维度 | 得分 | 权重 | 加权分 | 评审意见 |
|------|------|------|--------|----------|
| **依赖方向正确性** | 5/10 | 30% | 1.5 | Infrastructure 通过反射"偷渡"到 Features，这是架构腐败 |
| **切片边界完整性** | 7/10 | 25% | 1.75 | Features 组织合理，但存在跨切片耦合 |
| **接口抽象完整性** | 8/10 | 15% | 1.2 | 大部分服务有接口，但关键服务仍注入具体类型 |
| **DI 使用规范性** | 4/10 | 15% | 0.6 | 构造函数里 new 服务？这是 DI 反模式的教科书案例 |
| **可测试性** | 5/10 | 15% | 0.75 | 反射调用 + 静态服务 + 具体类型注入 = 测试噩梦 |

**综合得分：5.8/10** ⚠️ **需要改进**

### 1.2 问题严重性分布

```
┌─────────────────────────────────────────────────────────────────┐
│  🔴 Critical (架构腐败风险)     ████████████░░░░░░░░  3 个      │
│  🟠 Major (违反最佳实践)        ████████████████░░░░  5 个      │
│  🟡 Minor (代码质量问题)        ██████████░░░░░░░░░░  4 个      │
└─────────────────────────────────────────────────────────────────┘
```

---

## 二、Critical 问题详解：架构腐败的种子

### 🔴 C-1: 反射地狱 —— GlobalShortcutCoordinator

**问题位置**：[Infrastructure/Services/GlobalShortcutCoordinator.cs:56-91](file:///e:/01_代码仓库/Github/PromptMasterv6/Infrastructure/Services/GlobalShortcutCoordinator.cs#L56-L91)

**问题代码**：
```csharp
// 第 56-66 行
_hotkeyService.RegisterWindowHotkey("OcrHotkey", config.OcrHotkey, () =>
{
    System.Windows.Application.Current.Dispatcher.Invoke(() =>
    {
        // 😱 反射地狱开始
        var externalVmType = Type.GetType("PromptMasterv6.Features.ExternalTools.ExternalToolsViewModel, PromptMasterv6");
        if (externalVmType == null) return;
        
        var externalVM = _serviceProvider.GetService(externalVmType);
        var command = externalVM?.GetType().GetProperty("TriggerOcrCommand")?.GetValue(externalVM) as System.Windows.Input.ICommand;
        command?.Execute(null);
    });
});
```

**Jimmy Bogard 犀利点评**：

> "让我直说：**这不是解耦，这是自欺欺人**。
>
> 你以为用 `Type.GetType()` 和反射就避免了编译时依赖？不，你只是把编译时错误变成了运行时错误。当有人重构 `ExternalToolsViewModel`，把 `TriggerOcrCommand` 改名为 `OcrCommand`，你的代码不会报错——它会在用户按下热键时静默失败。
>
> 更糟糕的是，你把 Infrastructure 层变成了一个**全知全能的上帝**。它知道 Features 层有什么 ViewModel，知道它们有什么属性，知道如何调用它们的命令。这不是垂直切片——这是**分层架构的尸体**，你只是用反射掩盖了它的腐烂。
>
> **正确做法**：使用消息机制。Infrastructure 不应该知道 Features 的存在。"

**架构腐败路径**：
```
1. 开发者想解耦 → 使用反射
2. 反射调用成功 → 开发者觉得"很聪明"
3. 重构时改名 → 运行时静默失败
4. 用户报告 Bug → 开发者排查三天找不到原因
5. 代码变成"不能碰的区域" → 技术债务指数增长
```

**修复方案**：
```csharp
// 方案 A：使用 WeakReferenceMessenger（推荐）
// Core/Messages/TriggerOcrMessage.cs
public sealed record TriggerOcrMessage;

// Infrastructure/Services/GlobalShortcutCoordinator.cs
_hotkeyService.RegisterWindowHotkey("OcrHotkey", config.OcrHotkey, () =>
{
    WeakReferenceMessenger.Default.Send(new TriggerOcrMessage());
});

// Features/ExternalTools/ExternalToolsViewModel.cs
WeakReferenceMessenger.Default.Register<TriggerOcrMessage>(this, (_, _) =>
{
    TriggerOcrCommand.Execute(null);
});

// 方案 B：定义 IExternalToolsCoordinator 接口
// Core/Interfaces/IExternalToolsCoordinator.cs
public interface IExternalToolsCoordinator
{
    void TriggerOcr();
    void TriggerTranslate();
    void TriggerPinToScreen();
}
```

---

### 🔴 C-2: DI 反模式 —— 构造函数中 new 服务

**问题位置**：[Features/Main/MainViewModel.cs:149](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Main/MainViewModel.cs#L149)

**问题代码**：
```csharp
// 第 123-152 行
public MainViewModel(
    ISettingsService settingsService,
    IAiService aiService,
    WebDavDataService dataService,        // ❌ 具体类型
    FileDataService localDataService,     // ❌ 具体类型
    IGlobalKeyService keyService,
    IDialogService dialogService,
    ClipboardService clipboardService,    // ❌ 具体类型
    IWindowManager windowManager,
    IVariableService variableService,
    IContentConverterService contentConverterService,
    IWebTargetService webTargetService)
{
    // ...
    _hotkeyService = new HotkeyService();  // ❌❌❌ 直接 new！
    // ...
}
```

**Jimmy Bogard 犀利点评**：

> "你在构造函数里 `new HotkeyService()`？让我问你几个问题：
>
> 1. **你怎么测试这个 ViewModel？** 你无法 mock HotkeyService，每次测试都会创建真实的全局热键监听器。
> 2. **生命周期谁管理？** HotkeyService 可能需要释放资源，但你在 ViewModel 里 new 它，谁来调用 Dispose？
> 3. **你为什么用 DI？** 如果你要在构造函数里 new 服务，为什么不干脆全部 new？
>
> 这比不用 DI 还糟糕——你既用了 DI 容器，又绕过了它。这就像你买了保险箱，然后把钥匙放在门口的地垫下面。
>
> 还有，你注入 `WebDavDataService` 和 `FileDataService` 具体类型？你明明有 `IDataService` 接口！为什么不注入接口然后用某种方式区分实现？"

**问题分析表**：

| 注入参数 | 类型 | 问题 | 严重性 |
|----------|------|------|--------|
| `WebDavDataService dataService` | 具体类 | 违反 DIP，无法 mock | 🟠 Major |
| `FileDataService localDataService` | 具体类 | 违反 DIP，无法 mock | 🟠 Major |
| `ClipboardService clipboardService` | 具体类 | 无接口定义 | 🟠 Major |
| `_hotkeyService = new HotkeyService()` | 直接 new | DI 反模式，生命周期失控 | 🔴 Critical |

**修复方案**：
```csharp
// 1. 为 ClipboardService 创建接口
public interface IClipboardService
{
    void SetClipboard(string text);
    string? GetClipboard();
}

// 2. 使用 Keyed Services 区分 IDataService 实现
// App.xaml.cs
services.AddKeyedSingleton<IDataService, WebDavDataService>("cloud");
services.AddKeyedSingleton<IDataService, FileDataService>("local");

// MainViewModel.cs
public MainViewModel(
    [FromKeyedServices("cloud")] IDataService dataService,
    [FromKeyedServices("local")] IDataService localDataService,
    IClipboardService clipboardService,
    IHotkeyService hotkeyService)  // 注入！
```

---

### 🔴 C-3: Infrastructure 层的"上帝视角"

**问题位置**：[Infrastructure/Services/WindowManager.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Infrastructure/Services/WindowManager.cs)

**问题代码**：
```csharp
// 第 114-121 行：反射创建 Features 层的 Window
private Window CreateScreenCaptureOverlay(Bitmap screenBmp, Func<byte[], System.Windows.Rect, Task>? onCaptureProcessing)
{
    var overlayType = Type.GetType("PromptMasterv6.Features.ExternalTools.ScreenCaptureOverlay, PromptMasterv6");
    if (overlayType == null)
    {
        throw new InvalidOperationException("ScreenCaptureOverlay type not found");
    }
    var overlay = Activator.CreateInstance(overlayType, screenBmp, onCaptureProcessing) as Window;
    return overlay ?? throw new InvalidOperationException("Failed to create ScreenCaptureOverlay");
}

// 第 127-141 行：反射创建 TranslationPopup
// 第 296-301 行：反射调用 PinToScreenWindow
```

**Jimmy Bogard 犀利点评**：

> "WindowManager 知道所有窗口。它知道 `ScreenCaptureOverlay` 在 `Features.ExternalTools`，它知道 `TranslationPopup` 在哪里，它甚至知道 `PinToScreenWindow` 有一个静态方法 `PinToScreenAsync`。
>
> 这不是解耦——这是**伪装成解耦的紧耦合**。你用字符串代替了类型引用，但依赖关系依然存在。
>
> 在真正的垂直切片架构中，每个 Feature 应该管理自己的窗口生命周期。WindowManager 应该只提供基础设施（如窗口定位、模态对话框支持），而不是知道具体窗口类型。
>
> 你已经实现了 `IWindowRegistry`，这很好。但为什么 `WindowManager` 还要用反射创建窗口？你的 Registry 模式只用在了一半的地方。"

**架构问题可视化**：
```
┌─────────────────────────────────────────────────────────────────┐
│                     Infrastructure Layer                         │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                     WindowManager                         │    │
│  │  ┌─────────────────────────────────────────────────┐    │    │
│  │  │ "我知道所有窗口..."                               │    │    │
│  │  │ • ScreenCaptureOverlay 在 Features.ExternalTools │    │    │
│  │  │ • TranslationPopup 在 Features.ExternalTools     │    │    │
│  │  │ • PinToScreenWindow 在 Features.PinToScreen      │    │    │
│  │  │ • SettingsWindow 在 Features.Settings            │    │    │
│  │  └─────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────┘    │
│                              │                                   │
│                              │ 反射调用                          │
│                              ▼                                   │
└─────────────────────────────────────────────────────────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│ ExternalTools │    │  PinToScreen  │    │   Settings    │
│    Feature    │    │    Feature    │    │    Feature    │
└───────────────┘    └───────────────┘    └───────────────┘
```

---

## 三、Major 问题详解：最佳实践违规

### 🟠 M-1: GlobalUsings 暴露实现层

**问题位置**：[GlobalUsings.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/GlobalUsings.cs)

**问题代码**：
```csharp
global using PromptMasterv6.Core.Interfaces;        // ✅ 正确
global using PromptMasterv6.Core.Models;            // ✅ 正确
global using PromptMasterv6.Infrastructure.Services; // ❌ 暴露实现层
global using PromptMasterv6.Services;               // ❌ 命名空间不存在！
```

**Jimmy Bogard 点评**：

> "你把 `Infrastructure.Services` 全局暴露了？这意味着每个文件都可以直接使用 `LoggerService.Instance`、`HotkeyService` 等具体实现，而不需要显式 import。
>
> 这就像你在公司门口放了一个'免费糖果'的盒子，然后期望员工不会拿。人类行为告诉我们：**如果坏事很容易做，人们就会做**。
>
> 还有，`PromptMasterv6.Services` 这个命名空间根本不存在。这是编译警告，说明你的代码库有'死代码'——曾经存在但现在已删除的引用。"

---

### 🟠 M-2: 服务缺少接口定义

**问题服务清单**：

| 服务 | 有接口 | 使用位置 | 影响 |
|------|--------|----------|------|
| ClipboardService | ❌ | MainViewModel, ExternalToolsViewModel | 无法 mock，无法测试 |
| GlobalShortcutCoordinator | ❌ | App.xaml.cs | 无法替换实现 |
| LoggerService | ❌ | 全局使用 | 单例模式，难以测试 |
| ConfigService | ❌（静态） | SettingsService | 静态依赖 |
| LocalConfigService | ❌（静态） | SettingsService | 静态依赖 |

**Jimmy Bogard 点评**：

> "你说你用 DI，但你的核心服务没有接口？`ClipboardService` 直接注入到 ViewModel，`LoggerService` 是全局单例？
>
> 让我告诉你测试时会发生什么：
>
> ```csharp
> // 你想测试这个方法
> [RelayCommand]
> private void CopyCompiledText()
> {
>     var text = _variableService.CompileContent(SelectedFile?.Content, Variables, AdditionalInput);
>     if (string.IsNullOrWhiteSpace(text)) return;
>     _clipboardService.SetClipboard(text);  // 💥 测试时会真的写入剪贴板！
> }
> ```
>
> 你的单元测试会修改系统剪贴板。这不再是单元测试——这是集成测试。"

---

### 🟠 M-3: 跨功能 ViewModel 依赖

**问题位置**：[Features/Main/MainViewModel.cs:13-16](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Main/MainViewModel.cs#L13-L16)

**问题代码**：
```csharp
using PromptMasterv6.Features.Launcher;     // 跨功能依赖
using PromptMasterv6.Features.Sidebar;      // 跨功能依赖
using PromptMasterv6.Features.Workspace;    // 跨功能依赖
```

**Jimmy Bogard 点评**：

> "MainViewModel 直接依赖 `SidebarViewModel`、`WorkspaceViewModel`、`LauncherViewModel`？
>
> 这说明你的 Main Feature 不是一个真正的'切片'——它是一个**上帝 Feature**，知道其他所有 Feature 的存在。
>
> 在垂直切片架构中，Feature 之间应该通过消息通信，而不是直接依赖。如果 Main 需要控制 Sidebar，它应该发送消息，而不是直接持有 SidebarViewModel 的引用。"

---

### 🟠 M-4: Infrastructure 引用 Features.Messages

**问题位置**：
- [Infrastructure/Services/GlobalShortcutCoordinator.cs:3](file:///e:/01_代码仓库/Github/PromptMasterv6/Infrastructure/Services/GlobalShortcutCoordinator.cs#L3)
- [Infrastructure/Services/WindowManager.cs:13](file:///e:/01_代码仓库/Github/PromptMasterv6/Infrastructure/Services/WindowManager.cs#L13)

**问题代码**：
```csharp
using PromptMasterv6.Features.Shared.Messages;  // Infrastructure → Features
```

**Jimmy Bogard 点评**：

> "你说 Infrastructure 不依赖 Features？那这个 `using` 是什么？
>
> 好吧，`Shared.Messages` 确实是'共享'的，但它在 `Features/Shared/` 目录下。从依赖方向来看，这是 Infrastructure → Features。
>
> **正确做法**：把共享消息移到 `Core/Messages/` 或创建独立的 `Shared/` 层级（与 Core、Infrastructure、Features 平级）。"

---

### 🟠 M-5: 重复的转换器

**问题位置**：
- [Infrastructure/Converters/InverseBoolConverter.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Infrastructure/Converters/InverseBoolConverter.cs)
- [Infrastructure/Converters/InverseBooleanConverter.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Infrastructure/Converters/InverseBooleanConverter.cs)

**Jimmy Bogard 点评**：

> "两个文件做同样的事情？这说明你的代码库缺乏'代码卫生'习惯。
>
> 这不是大问题，但它是一个信号：**如果连转换器都有重复，业务逻辑里会有多少重复？**"

---

## 四、架构改进路线图

### Phase 1: 止血（紧急，1-2 天）

| 优先级 | 任务 | 影响 | 预估工时 |
|--------|------|------|----------|
| P0 | 消除 GlobalShortcutCoordinator 中的反射调用 | 架构腐败风险 | 3h |
| P0 | 修复 MainViewModel 中的 `new HotkeyService()` | DI 反模式 | 1h |
| P0 | 修复 GlobalUsings.cs（移除不存在的命名空间） | 编译警告 | 0.5h |

### Phase 2: 重构（重要，3-5 天）

| 优先级 | 任务 | 影响 | 预估工时 |
|--------|------|------|----------|
| P1 | 为 ClipboardService 创建接口 | 可测试性 | 1h |
| P1 | 使用 Keyed Services 区分 IDataService 实现 | DIP 合规 | 2h |
| P1 | 将 Shared/Messages 移至 Core/Messages | 依赖方向 | 1h |
| P1 | 消除 WindowManager 中的反射调用 | 架构一致性 | 3h |

### Phase 3: 完善（持续）

| 优先级 | 任务 | 影响 | 预估工时 |
|--------|------|------|----------|
| P2 | 为 LoggerService 创建接口（可选） | 可测试性 | 2h |
| P2 | 消除 MainViewModel 的跨功能依赖 | 切片边界 | 4h |
| P2 | 合并重复转换器 | 代码卫生 | 0.5h |

---

## 五、Jimmy Bogard 最终评语

> "让我总结一下这个项目的状态：
>
> **你做对的事情**：
> - Features 按功能组织，这是垂直切片的核心
> - Messages 用于跨功能通信，方向正确
> - 大部分服务有接口定义
> - Window Registry 模式是个好的开始
> - Settings 拆分为子 ViewModel，职责清晰
>
> **你做错的事情**：
> - **反射不是解耦**，它只是把编译时错误变成运行时错误
> - **DI 反模式**：构造函数里 new 服务，这是不可接受的
> - **上帝服务**：GlobalShortcutCoordinator 和 WindowManager 知道太多
> - **依赖方向违规**：Infrastructure 通过反射'偷渡'到 Features
>
> **我的建议**：
>
> 你不需要重写这个项目。架构问题虽然存在，但都是可修复的。关键是要**停止用反射掩盖问题**。
>
> 垂直切片架构的核心是：**每个用例独立**。当你发现 Infrastructure 需要知道 Features 的存在时，停下来问自己：'这个逻辑应该在哪里？'
>
> 答案通常是：**在 Feature 里**。
>
> 把热键触发逻辑移到 Feature，把窗口创建逻辑移到 Feature，让 Infrastructure 只提供基础设施。这才是真正的垂直切片。"

---

## 六、问题清单速查表

| ID | 级别 | 问题 | 位置 | 状态 |
|----|------|------|------|------|
| C-1 | 🔴 Critical | 反射地狱 - GlobalShortcutCoordinator | Infrastructure/Services/GlobalShortcutCoordinator.cs | ✅ 已修复 |
| C-2 | 🔴 Critical | DI 反模式 - 构造函数中 new 服务 | Features/Main/MainViewModel.cs:149 | ✅ 已修复 |
| C-3 | 🔴 Critical | Infrastructure 上帝视角 | Infrastructure/Services/WindowManager.cs | ✅ 已修复 |
| M-1 | 🟠 Major | GlobalUsings 暴露实现层 | GlobalUsings.cs | ✅ 已修复 |
| M-2 | 🟠 Major | 服务缺少接口定义 | Infrastructure/Services/*.cs | ✅ 已修复 |
| M-3 | 🟠 Major | 跨功能 ViewModel 依赖 | Features/Main/MainViewModel.cs | ✅ 已修复 |
| M-4 | 🟠 Major | Infrastructure 引用 Features.Messages | Infrastructure/Services/*.cs | ✅ 已修复 |
| M-5 | 🟠 Major | 重复的转换器 | Infrastructure/Converters/ | ✅ 已修复 |

---

*报告生成时间：2026-03-09*
*评审人：Jimmy Bogard 视角（AI 模拟）*
*评审风格：专业深度 + 犀利视角*
