# PromptMaster v6 垂直切片架构局部深度评审报告

> **评审视角**：Jimmy Bogard（垂直切片架构创始人，MediatR/Carter 作者）
> **评审日期**：2026-03-09
> **评审范围**：局部 Features 深度评审
> **评审风格**：专业深度 + 犀利视角

---

## 零、开篇语：Jimmy Bogard 的审视

> "我看过很多项目，它们声称用了垂直切片架构，但当你打开代码，发现只是把文件夹重命名了一下。
>
> 今天我要深入每个 Feature 的内部，看看它们是否真正遵循了垂直切片的核心原则：**每个用例独立理解、独立修改、独立测试**。
>
> 垂直切片不是文件夹组织——它是让代码按业务能力组织，而不是按技术层组织。如果一个 ViewModel 有 12 个依赖、14 个命令、管理文件/文件夹/编辑器/变量/Web目标/快捷键/同步——那不是切片，那是**披萨**，什么都有，但什么都不纯粹。"

---

## 一、Feature 健康度总评

### 1.1 评分矩阵

| Feature | 代码行数 | 依赖数 | 命令数 | 健康度 | 评级 |
|---------|---------|--------|--------|--------|------|
| **Main** | 692 | 12 | 14 | 3/10 | 🔴 危急 |
| **Settings** | 744+子VM | 7+5 | 21 | 5/10 | 🟠 需改进 |
| **ExternalTools** | 564 | 8 | 3 | 7/10 | 🟡 良好 |
| **Launcher** | 217 | 3 | 1 | 9/10 | 🟢 优秀 |
| **Sidebar** | 164 | 1 | 5 | 8/10 | 🟢 良好 |
| **Workspace** | 377 | 8 | 8 | 4/10 | 🟠 需改进 |
| **Shared Services** | 189 | 2-3 | N/A | 6/10 | 🟡 位置不当 |

**整体架构健康度：5.5/10** ⚠️ **需要改进**

---

## 二、🔴 危急问题：MainViewModel - 上帝视图模型

### 2.1 问题诊断

**位置**：[Features/Main/MainViewModel.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Main/MainViewModel.cs)

**Jimmy Bogard 犀利点评**：

> "让我直说：**这不是 ViewModel，这是应用程序**。
>
> 你有 12 个构造函数依赖？让我数数你做了什么：
> - 文件管理（`Files`, `SelectedFile`）
> - 文件夹管理（`Folders`, `SelectedFolder`）
> - 编辑器状态（`IsEditMode`, `PreviewContent`）
> - 变量解析（`Variables`, `HasVariables`）
> - Web 目标发送（`SendDefaultWebTargetCommand`）
> - 快捷键管理（`_hotkeyService`）
> - 同步状态（`SyncTimeDisplay`）
> - 定时器（`_timer`）
> - Reactive 扩展（`_saveSubject`）
>
> 你还持有两个子 ViewModel 的引用（`SidebarVM`, `WorkspaceVM`），但你没有委托任何职责给它们！
>
> **这不是垂直切片，这是水平大杂烩**。"

### 2.2 代码证据

```csharp
// MainViewModel.cs 第 35-46 行 - 12 个依赖！
private readonly IDataService _dataService;
private readonly IDataService _localDataService;
private readonly IGlobalKeyService _keyService;
private readonly IAiService _aiService;
private readonly IDialogService _dialogService;
private readonly IClipboardService _clipboardService;
private readonly IWindowManager _windowManager;
private readonly ISettingsService _settingsService;
private readonly IHotkeyService _hotkeyService;
private readonly IVariableService _variableService;
private readonly IContentConverterService _contentConverterService;
private readonly IWebTargetService _webTargetService;

// 第 74-75 行 - 持有子 ViewModel 但未委托
[ObservableProperty] private SidebarViewModel? sidebarVM;
[ObservableProperty] private WorkspaceViewModel? workspaceVM;
```

### 2.3 职责分析图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          MainViewModel (692 行)                          │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │ 文件管理    │  │ 文件夹管理  │  │ 编辑器状态  │  │ 变量解析    │    │
│  │ 14个命令    │  │ Folders     │  │ IsEditMode  │  │ Variables   │    │
│  │ Files       │  │ SelectedFolder│ │ PreviewContent│ │ HasVariables│   │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘    │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │ Web目标     │  │ 快捷键      │  │ 同步状态    │  │ 定时器      │    │
│  │ SendDefault │  │ HotkeyService│ │ SyncTime    │  │ Dispatcher  │    │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘    │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ 子 ViewModel 引用（未真正使用）                                    │   │
│  │ SidebarVM ───> 空壳                                               │   │
│  │ WorkspaceVM ─> 空壳                                               │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.4 重构建议

**拆分方案**：

```
MainViewModel (协调者，~150 行)
├── FileListViewModel      - 文件列表管理 (~200 行)
├── FolderListViewModel    - 文件夹管理 (合并 SidebarViewModel, ~150 行)
├── EditorViewModel        - 编辑器 + 预览 (~200 行)
├── VariablePanelViewModel - 变量面板 (~100 行)
└── WebTargetViewModel     - Web 目标发送 (~100 行)
```

**依赖对比**：

| 重构前 | 重构后 |
|--------|--------|
| MainViewModel: 12 依赖 | MainViewModel: 5 子VM |
| 无测试可能 | 每个子VM 可独立测试 |

---

## 三、🟠 需改进问题：SettingsViewModel - 聚合器混乱

### 3.1 问题诊断

**位置**：[Features/Settings/SettingsViewModel.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Settings/SettingsViewModel.cs)

**Jimmy Bogard 犀利点评**：

> "你的 `SettingsViewModel` 有两个身份危机：
>
> 1. **它既是聚合器**：持有 5 个子 ViewModel 的引用
> 2. **它又是业务逻辑实现者**：包含 AI 模型管理、同步恢复、LaunchBar 管理等 21 个命令
>
> 这就像一个经理，既想管理团队，又想亲自写代码。结果是什么都做不好。
>
> 更糟糕的是，你直接引用了 `MainViewModel`：
>
> ```csharp
> private MainViewModel? _mainViewModel;
> public void SetMainViewModel(MainViewModel mainViewModel)
> {
>     _mainViewModel = mainViewModel;
>     SyncVM.SetMainViewModel(mainViewModel);
> }
> ```
>
> 这违反了垂直切片的核心原则：**Feature 之间不应该有直接依赖**。你应该用消息机制。"

### 3.2 子 ViewModel 职责重叠

| 功能 | SettingsViewModel | 子 ViewModel |
|------|-------------------|--------------|
| AI 模型测试 | ✅ `TestModelCommand` | ✅ `AiModelsViewModel.TestModelCommand` |
| AI 模型添加 | ✅ `AddModelCommand` | ✅ `AiModelsViewModel.AddModelCommand` |
| 同步恢复 | ✅ `RestoreFromBackupCommand` | ✅ `SyncViewModel.RestoreFromBackupCommand` |
| API 凭证管理 | ✅ 直接实现 | ✅ `ApiCredentialsViewModel` |

### 3.3 重复代码问题

**ApiProvidersViewModel vs ApiCredentialsViewModel**：

```
ApiProvidersViewModel.cs:  589 行
ApiCredentialsViewModel.cs: 620 行
重复度: ~95%
```

两者都包含：
- 百度/腾讯/有道/Google 凭证管理
- 相同的测试方法
- 相同的 `CreateTestImage()` 辅助方法

**Jimmy Bogard 点评**：

> "你有两个几乎完全相同的 ViewModel？这说明你的代码审查流程有问题。
>
> 删除一个，保留 `ApiCredentialsViewModel`。如果两个地方需要不同的展示，那是 View 的问题，不是 ViewModel 的问题。"

---

## 四、🟠 需改进问题：WorkspaceViewModel - 代码重复

### 4.1 问题诊断

**位置**：[Features/Workspace/WorkspaceViewModel.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Workspace/WorkspaceViewModel.cs)

**Jimmy Bogard 犀利点评**：

> "`WorkspaceViewModel` 和 `MainViewModel` 有大量重复代码。让我对比一下：
>
> | 功能 | MainViewModel | WorkspaceViewModel |
> |------|---------------|-------------------|
> | 文件选择处理 | `OnSelectedFileChanged` | `OnSelectedFileChanged` |
> | 变量解析 | `SafeParseVariables` | `SafeParseVariables` |
> | 编辑模式切换 | `ToggleEditModeCommand` | `ToggleEditModeCommand` |
> | Web 目标发送 | `SendDefaultWebTargetCommand` | `SendDefaultWebTargetCommand` |
> | GitHub 搜索 | `SearchOnGitHubCommand` | `SearchOnGitHubCommand` |
>
> 这不是重构，这是**复制粘贴**。
>
> 更糟糕的是，`WorkspaceViewModel` 直接注入了 `ClipboardService`（具体类）而不是 `IClipboardService`（接口）。你之前的修复白做了。"

### 4.2 代码证据

```csharp
// WorkspaceViewModel.cs 第 26 行 - 具体类型注入！
private readonly ClipboardService _clipboardService;

// 对比 MainViewModel.cs 第 40 行 - 接口注入
private readonly IClipboardService _clipboardService;
```

### 4.3 根本原因分析

```
┌─────────────────────────────────────────────────────────────────┐
│                        设计意图                                  │
│  MainViewModel 应该是协调者，WorkspaceViewModel 负责编辑器逻辑   │
├─────────────────────────────────────────────────────────────────┤
│                        实际情况                                  │
│  MainViewModel 保留了所有功能                                    │
│  WorkspaceViewModel 复制了部分功能                               │
│  两者同时存在，职责混乱                                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 五、🟢 优秀案例：LauncherViewModel - 切片典范

### 5.1 为什么它是典范

**位置**：[Features/Launcher/LauncherViewModel.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/Launcher/LauncherViewModel.cs)

**Jimmy Bogard 点评**：

> "这才是垂直切片应该有的样子：
>
> - **3 个依赖**：`ILauncherService`, `ISettingsService`, `IWindowManager`
> - **1 个命令**：`ExecuteItemCommand`
> - **无跨 Feature 依赖**
> - **职责单一**：启动应用程序
>
> 你可以独立理解这个 ViewModel，独立修改它，独立测试它。这就是垂直切片的核心。"

### 5.2 代码证据

```csharp
// LauncherViewModel.cs - 简洁的构造函数
public LauncherViewModel(
    ILauncherService launcherService,
    ISettingsService settingsService,
    IWindowManager windowManager)
{
    _launcherService = launcherService;
    _settingsService = settingsService;
    _windowManager = windowManager;
}

// 单一职责：执行启动项
[RelayCommand]
private void ExecuteItem(LauncherItem? item)
{
    if (item == null) return;
    // ... 执行逻辑
}
```

### 5.3 接口设计良好

```csharp
// ILauncherService.cs - 清晰的接口定义
public interface ILauncherService
{
    Task<List<LauncherItem>> GetItemsAsync(IEnumerable<string> paths);
    void ClearCache();
}
```

---

## 六、🟡 位置不当：Shared Services

### 6.1 问题诊断

**位置**：`Features/Shared/Services/`

**Jimmy Bogard 犀利点评**：

> "你有三个服务放在 `Features/Shared/Services/`：
>
> - `VariableService.cs` (60 行)
> - `ContentConverterService.cs` (33 行)
> - `WebTargetService.cs` (96 行)
>
> 它们的接口定义在 `Core/Interfaces/`，但实现放在 `Features/Shared/Services/`。
>
> **问题**：这些是基础设施服务，不是 Feature 特定逻辑。它们应该放在 `Infrastructure/Services/`。
>
> `Features/` 目录应该只包含按业务能力组织的垂直切片，不是共享服务的垃圾场。"

### 6.2 正确的目录结构

```
❌ 错误：
Features/
├── Shared/
│   └── Services/          <- 不应该在这里
│       ├── VariableService.cs
│       ├── ContentConverterService.cs
│       └── WebTargetService.cs

✅ 正确：
Infrastructure/
├── Services/
│   ├── VariableService.cs
│   ├── ContentConverterService.cs
│   └── WebTargetService.cs
```

---

## 七、跨 Feature 依赖分析

### 7.1 依赖矩阵

```
                Main  Settings  ExternalTools  Launcher  Sidebar  Workspace
Main             -       X           X            X         -         -
Settings         X       -           X            -         -         -
ExternalTools    -       -           -            -         -         -
Launcher         -       -           -            -         -         -
Sidebar          X       -           -            -         -         -
Workspace        -       -           -            -         -         -

X = 直接依赖（通过 using 或类型引用）
```

### 7.2 问题依赖详解

| 源 Feature | 目标 Feature | 依赖方式 | 问题 |
|------------|--------------|----------|------|
| Settings | Main | `MainViewModel` 类型引用 | 违反 Feature 边界 |
| Main | Sidebar | `SidebarViewModel` 属性 | 应该用消息 |
| Main | Workspace | `WorkspaceViewModel` 属性 | 应该用消息 |
| Main | Launcher | `TriggerLauncherMessage` | ✅ 正确（消息） |

### 7.3 Jimmy Bogard 的建议

> "Feature 之间应该通过消息通信，而不是直接依赖。
>
> **正确做法**：
> - `SettingsViewModel` 需要通知 `MainViewModel`？发送 `SettingsChangedMessage`
> - `MainViewModel` 需要控制 `SidebarViewModel`？发送 `SelectFolderMessage`
>
> **错误做法**：
> - `SettingsViewModel` 直接持有 `MainViewModel` 引用
> - `MainViewModel` 直接调用 `SidebarViewModel` 的方法"

---

## 八、硬编码问题

### 8.1 ExternalToolsViewModel 中的硬编码 Prompt

**位置**：[Features/ExternalTools/ExternalToolsViewModel.cs](file:///e:/01_代码仓库/Github/PromptMasterv6/Features/ExternalTools/ExternalToolsViewModel.cs)

```csharp
// 约 15 行硬编码的 OCR Prompt
string ocrSystemPrompt = @"# 角色任务
你是一个专业的OCR识别助手...

# 输出要求
...";

// 约 30 行硬编码的视觉翻译 Prompt
string visionPrompt = @"# Role
You are a professional translation assistant...
...";
```

**Jimmy Bogard 点评**：

> "你在代码里硬编码了 45 行 Prompt？
>
> 如果产品经理想修改 Prompt，他需要：
> 1. 找到开发人员
> 2. 开发人员打开代码
> 3. 修改字符串
> 4. 重新编译
> 5. 重新部署
>
> **正确做法**：把 Prompt 提取到配置文件或资源文件。让非开发人员也能修改。"

---

## 九、重构优先级

### 9.1 紧急 (P0)

| 问题 | 影响 | 预估工时 |
|------|------|----------|
| 拆分 MainViewModel | 架构健康度 +3 | 2 天 |
| 删除重复的 ApiProvidersViewModel | 代码减少 600 行 | 1 小时 |
| 修复 WorkspaceViewModel 的具体类型注入 | DI 合规 | 10 分钟 |

### 9.2 重要 (P1)

| 问题 | 影响 | 预估工时 |
|------|------|----------|
| 解决 SettingsViewModel 与子 VM 的职责重叠 | 架构清晰 | 4 小时 |
| 移动 Shared Services 到 Infrastructure | 目录结构正确 | 30 分钟 |
| 消除 Settings→Main 的直接依赖 | Feature 边界 | 2 小时 |

### 9.3 改进 (P2)

| 问题 | 影响 | 预估工时 |
|------|------|----------|
| 提取硬编码 Prompt 到配置 | 可维护性 | 2 小时 |
| 统一消息命名空间 | 代码一致性 | 1 小时 |

---

## 十、Jimmy Bogard 最终评语

> "让我总结一下这个项目的 Feature 层状态：
>
> **你做对的事情**：
> - Launcher Feature 是垂直切片的典范
> - Sidebar Feature 依赖极少，职责单一
> - 消息机制用于跨 Feature 通信（部分）
> - 子 ViewModel 拆分已开始（Settings）
>
> **你做错的事情**：
> - **MainViewModel 是上帝视图模型**：12 个依赖，14 个命令，什么都管
> - **SettingsViewModel 身份混乱**：既是聚合器又是实现者
> - **代码重复严重**：WorkspaceViewModel 复制 MainViewModel，ApiProvidersViewModel 复制 ApiCredentialsViewModel
> - **Feature 边界被打破**：Settings 直接引用 Main
>
> **我的建议**：
>
> 1. **立即拆分 MainViewModel**。这是架构债务的源头。
> 2. **删除重复代码**。两个相同的 ViewModel 是不可接受的。
> 3. **用消息替代直接依赖**。Feature 之间不应该知道彼此的存在。
>
> 垂直切片架构的核心是：**每个 Feature 是一个独立的业务单元**。当你发现一个 ViewModel 需要 12 个依赖时，停下来问自己：'这个 Feature 的边界在哪里？'
>
> 如果答案是'这个 ViewModel 就是整个应用'，那你没有垂直切片，你只有一个大杂烩。"

---

## 十一、问题清单速查表

| ID | 级别 | 问题 | 位置 | 状态 |
|----|------|------|------|------|
| L-2 | 🔴 Critical | WorkspaceViewModel 具体类型注入 | Features/Workspace/WorkspaceViewModel.cs:26 | ✅ 已修复 |
| L-3 | 🟠 Major | SettingsViewModel 聚合器混乱 | Features/Settings/SettingsViewModel.cs | ⏳ 待修复 |
| L-4 | 🟠 Major | ApiProvidersViewModel 重复代码 | Features/Settings/ApiProviders/ | ✅ 已修复 |
| L-5 | 🟠 Major | Settings→Main 直接依赖 | Features/Settings/SettingsViewModel.cs:33 | ✅ 已修复 |
| L-6 | 🟡 Minor | Shared Services 位置不当 | Features/Shared/Services/ | ✅ 已修复 |
| L-7 | 🟡 Minor | 硬编码 Prompt | Features/ExternalTools/ExternalToolsViewModel.cs | ✅ 已修复 |

---

*报告生成时间：2026-03-09*
*评审人：Jimmy Bogard 视角（AI 模拟）*
*评审风格：专业深度 + 犀利视角*
