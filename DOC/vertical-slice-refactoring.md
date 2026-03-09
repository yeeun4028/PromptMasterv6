# 垂直切片架构重构 - Jimmy Bogard 方法论

## 核心原则

> "Don't organize by technical layer. Organize by feature." — Jimmy Bogard

垂直切片架构的核心思想：
1. **每个功能自包含**：一个功能 = 一个文件夹，包含其所需的 View、ViewModel、Service、Model、Message
2. **按功能解耦，而非按层解耦**：避免传统的 View/ViewModel/Service 分层
3. **Feature Folders**：用文件夹组织功能，而非技术类型

---

## 当前架构诊断

### 问题清单

| 问题 | 严重程度 | 描述 |
|------|----------|------|
| MainViewModel 臃肿 | 🔴 严重 | 894 行，承担文件夹管理、文件管理、变量解析、热键、同步等过多职责 |
| SettingsViewModel 臃肿 | 🔴 严重 | 1307 行，混合了 AI 配置、OCR 配置、翻译配置、同步管理、启动器配置等 |
| Messages 集中管理 | 🟡 中等 | `ViewModels/Messages/` 集中存放消息，违反垂直切片原则 |
| 对话框散落 | 🟡 中等 | 根目录 `Views/` 下的对话框应归入对应 Feature |
| 代码重复 | 🟡 中等 | MainViewModel 和 WorkspaceViewModel 有重复逻辑（变量解析、HTML转Markdown） |

### MainViewModel 职责分析（894 行）

```
MainViewModel 当前承担的职责：
├── 文件夹管理（Folders, SelectedFolder, MoveFileToFolder）
├── 文件管理（Files, SelectedFile, DeleteFile, RenameFile）
├── 变量解析（Variables, ParseVariablesRealTime, CompileContent）
├── 内容预览（PreviewContent, ConvertHtmlToMarkdown）
├── 编辑模式（IsEditMode, ToggleEditMode）
├── 热键管理（UpdateWindowHotkeys, ToggleMainWindow）
├── 同步管理（PerformLocalBackup, RequestSave）
├── 网页目标（OpenWebTarget, SendDefaultWebTarget）
├── 启动器触发（HandleLauncherTriggered）
└── 消息订阅（7 种 WeakReferenceMessenger 消息）
```

**诊断**：这是典型的"上帝对象"反模式。一个 ViewModel 承担了至少 10 种职责。

### SettingsViewModel 职责分析（1307 行）

```
SettingsViewModel 当前承担的职责：
├── AI 模型管理（AddAiModel, DeleteAiModel, TestAiConnection）
├── AI 翻译配置（SaveAiTranslationConfig, TestAiTranslationConnection）
├── 百度 API 配置（TestBaiduOcr, TestBaiduTranslate）
├── 腾讯云 API 配置（TestTencentOcr, TestTencentCloud）
├── Google API 配置（TestGoogle）
├── 有道 API 配置（TestYoudao）
├── 同步与恢复（ManualBackup, ManualRestore, ManualLocalRestore）
├── 启动器配置（AddLauncherSearchPath, RemoveLauncherSearchPath）
├── Launch Bar 配置（AddLaunchBarItem, RemoveLaunchBarItem）
├── 配置导入导出（ExportConfig, ImportConfig）
└── 日志管理（OpenLogFolder, ClearLogs）
```

**诊断**：设置页面混合了至少 11 种不同领域的配置，每个领域都应该独立成 Feature。

---

## 目标架构

### 目录结构

```
Features/
├── PromptManagement/              # 提示词管理（核心功能）
│   ├── PromptList/
│   │   ├── PromptListViewModel.cs
│   │   └── PromptListView.xaml
│   ├── PromptEditor/
│   │   ├── PromptEditorViewModel.cs
│   │   └── PromptEditorView.xaml
│   ├── Models/
│   │   └── PromptItem.cs
│   ├── Services/
│   │   └── VariableService.cs       # 变量解析逻辑
│   └── Messages/
│       └── RequestSelectFileMessage.cs
│
├── FolderManagement/              # 文件夹管理
│   ├── FolderListViewModel.cs
│   ├── FolderListView.xaml
│   ├── Models/
│   │   └── FolderItem.cs
│   └── Messages/
│       └── FolderSelectionChangedMessage.cs
│
├── WebTargets/                    # 网页目标发送
│   ├── WebTargetService.cs
│   ├── Models/
│   │   └── WebTarget.cs
│   └── Messages/
│       └── WebTargetRequestMessage.cs
│
├── Settings/                      # 设置（按子功能拆分）
│   ├── AiModels/
│   │   ├── AiModelsViewModel.cs
│   │   └── AiModelsView.xaml
│   ├── ApiProviders/
│   │   ├── ApiProvidersViewModel.cs
│   │   └── ApiProvidersView.xaml
│   ├── Sync/
│   │   ├── SyncViewModel.cs
│   │   ├── SyncView.xaml
│   │   └── BackupSelectionDialog.xaml
│   ├── Launcher/
│   │   ├── LauncherSettingsViewModel.cs
│   │   └── LauncherSettingsView.xaml
│   └── SettingsContainerViewModel.cs  # 容器，协调子 VM
│
├── Launcher/                      # 启动器（已较完善）
│   ├── LauncherViewModel.cs
│   ├── LauncherWindow.xaml
│   ├── LauncherService.cs
│   └── Messages/
│       └── TriggerLauncherMessage.cs
│
├── Translation/                   # 翻译功能
│   ├── TranslationViewModel.cs
│   ├── TranslationPopup.xaml
│   └── Services/
│       └── TranslationService.cs
│
├── Sync/                          # 同步功能
│   ├── SyncService.cs
│   └── Messages/
│       └── RequestBackupMessage.cs
│
├── Main/                          # 主窗口（协调者）
│   ├── MainViewModel.cs           # 精简后，仅协调
│   └── MainWindow.xaml
│
├── Sidebar/                       # 侧边栏
│   ├── SidebarViewModel.cs
│   └── SidebarView.xaml
│
├── Workspace/                     # 工作区
│   ├── WorkspaceViewModel.cs
│   └── WorkspaceView.xaml
│
└── Shared/                        # 共享组件
    ├── Converters/
    ├── Behaviors/
    └── Services/
        └── ContentConverter.cs    # HTML ↔ Markdown
```

---

## 重构阶段

### Phase 1: 提取共享服务（低风险）
- [ ] 提取 `VariableService`：变量解析逻辑
- [ ] 提取 `ContentConverterService`：HTML ↔ Markdown 转换
- [ ] 提取 `WebTargetService`：网页目标发送逻辑

### Phase 2: 拆分 MainViewModel（中风险）
- [ ] 创建 `FolderManagement` Feature
- [ ] 创建 `PromptManagement` Feature（含 Editor + List）
- [ ] 创建 `WebTargets` Feature
- [ ] 精简 MainViewModel 为协调者角色

### Phase 3: 拆分 SettingsViewModel（中风险）
- [ ] 创建 `Settings/AiModels` 子 Feature
- [ ] 创建 `Settings/ApiProviders` 子 Feature
- [ ] 创建 `Settings/Sync` 子 Feature
- [ ] 创建 `Settings/Launcher` 子 Feature
- [ ] 创建 `SettingsContainerViewModel` 协调子 VM

### Phase 4: 迁移 Messages（低风险）
- [ ] 将 `ViewModels/Messages/` 中的消息迁移到对应 Feature

### Phase 5: 迁移对话框（低风险）
- [ ] `BackupSelectionDialog` → `Features/Sync/`
- [ ] `ConfirmationDialog` → `Features/Shared/`
- [ ] `PinToScreenWindow` → `Features/PinToScreen/`

---

## 进展记录

### 2026-03-09: Phase 1 & 2 完成

#### Phase 1: 提取共享服务 ✅
创建了 `Features/Shared/Services/` 目录，提取了 3 个共享服务：

| 服务 | 接口 | 职责 |
|------|------|------|
| VariableService | IVariableService | 变量解析、内容编译 |
| ContentConverterService | IContentConverterService | HTML ↔ Markdown 转换 |
| WebTargetService | IWebTargetService | 网页目标发送逻辑 |

**新增文件：**
- `Features/Shared/Services/VariableService.cs`
- `Features/Shared/Services/ContentConverterService.cs`
- `Features/Shared/Services/WebTargetService.cs`
- `Core/Interfaces/IClipboardService.cs`

**DI 注册：** 已在 `App.xaml.cs` 中注册所有新服务

#### Phase 2: 重构 ViewModel ✅

| ViewModel | 重构前行数 | 重构后行数 | 减少 |
|-----------|-----------|-----------|------|
| MainViewModel | 894 | 717 | -177 (20%) |
| WorkspaceViewModel | 536 | 377 | -159 (30%) |

**移除的重复代码：**
- `VariableRegex` 静态字段（2处）
- `HtmlTagRegex` 静态字段（2处）
- `ParseVariablesRealTime` 方法（2处）
- `CompileContent` 方法（2处）
- `ConvertHtmlToMarkdown` 方法（2处）
- `ContainsHtml` 方法（2处）
- `OpenWebTarget` 方法（2处）
- `SendDefaultWebTarget` 方法（2处）

### 2026-03-09: 架构分析与规划
- ✅ 完成项目架构现状分析
- ✅ 识别 MainViewModel 职责边界（10+ 职责）
- ✅ 识别 SettingsViewModel 职责边界（11+ 职责）
- ✅ 设计目标架构
- ✅ Phase 1: 提取共享服务
- ✅ Phase 2: 重构 ViewModel 使用新服务
- ✅ Phase 3: 发现 Settings 子 Feature 已存在，修复无效 DI 注册
- ✅ Phase 4: 迁移 Messages 到各 Feature

---

## Phase 5: 清理工作

### 已清理的文件
- `ViewModels/PromptGroup.cs` - 未使用的数据类
- `Views/sync_new.txt` - 无关文本文件

### 已清理的命名空间引用
- `App.xaml.cs` - 移除 `using PromptMasterv6.ViewModels`
- `SettingsView.xaml.cs` - 移除 `using PromptMasterv6.ViewModels`
- `SettingsWindow.xaml.cs` - 移除 `using PromptMasterv6.ViewModels`
- `App.xaml` - 移除 `xmlns:vm` 和 `xmlns:views`

### 残留文件（已完成迁移）

| 文件 | 迁移目标 | 状态 |
|------|----------|------|
| `PinToScreenWindow.xaml` | `Features/PinToScreen/` | ✅ 已迁移 |
| `ConfirmationDialog.xaml` | `Features/Shared/Dialogs/` | ✅ 已迁移 |
| `BackupSelectionDialog.xaml` | `Features/Settings/` | ✅ 已迁移 |
| `Dialogs/OcrNotConfiguredDialog.xaml` | `Features/ExternalTools/Dialogs/` | ✅ 已迁移 |

---

## Phase 6: 对话框迁移详情

### 迁移的文件

| 原位置 | 新位置 | 更新的引用文件 |
|--------|--------|----------------|
| `Views/PinToScreenWindow.xaml(.cs)` | `Features/PinToScreen/` | `Infrastructure/Services/WindowManager.cs` |
| `Views/ConfirmationDialog.xaml(.cs)` | `Features/Shared/Dialogs/` | `Features/Main/MainWindow.xaml.cs` |
| `Views/BackupSelectionDialog.xaml(.cs)` | `Features/Settings/` | `Features/Settings/SettingsViewModel.cs` |
| `Views/Dialogs/OcrNotConfiguredDialog.xaml(.cs)` | `Features/ExternalTools/Dialogs/` | `Infrastructure/Services/DialogService.cs` |

### 更新的命名空间

| 文件 | 旧命名空间 | 新命名空间 |
|------|------------|------------|
| PinToScreenWindow | `PromptMasterv6.Views` | `PromptMasterv6.Features.PinToScreen` |
| ConfirmationDialog | `PromptMasterv6.Views` | `PromptMasterv6.Features.Shared.Dialogs` |
| BackupSelectionDialog | `PromptMasterv6.Views` | `PromptMasterv6.Features.Settings` |
| OcrNotConfiguredDialog | `PromptMasterv6.Views.Dialogs` | `PromptMasterv6.Features.ExternalTools.Dialogs` |

---

## Phase 7: SettingsViewModel 拆分详情

### 拆分策略

SettingsViewModel 被拆分为多个子 ViewModel，每个负责单一职责：

| 子 ViewModel | 职责 | 行数 |
|--------------|------|------|
| `AiModelsViewModel` | AI 模型管理、测试 | ~170 行 |
| `ApiProvidersViewModel` | 百度/腾讯/有道/Google API 配置和测试 | ~590 行 |
| `SyncViewModel` | 同步、恢复、备份、导入导出、日志管理 | ~330 行 |
| `LauncherSettingsViewModel` | 启动器、启动栏配置 | ~110 行 |

### 聚合模式

SettingsViewModel 作为聚合器，通过构造函数注入子 ViewModel：

```csharp
public partial class SettingsViewModel : ObservableObject
{
    public AiModelsViewModel AiModelsVM { get; }
    public ApiProvidersViewModel ApiProvidersVM { get; }
    public SyncViewModel SyncVM { get; }
    public LauncherSettingsViewModel LauncherSettingsVM { get; }
    
    // 保持 XAML 绑定兼容
    public SettingsViewModel SettingsVM => this;
    
    public SettingsViewModel(
        // ... 其他依赖
        AiModelsViewModel aiModelsVM,
        ApiProvidersViewModel apiProvidersVM,
        SyncViewModel syncVM,
        LauncherSettingsViewModel launcherSettingsVM)
    {
        AiModelsVM = aiModelsVM;
        ApiProvidersVM = apiProvidersVM;
        SyncVM = syncVM;
        LauncherSettingsVM = launcherSettingsVM;
    }
}
```

---

## Phase 8: XAML 绑定兼容性修复

### 问题

XAML 中大量绑定使用 `{Binding SettingsVM.XXXCommand}` 路径，直接切换到子 ViewModel 会破坏现有绑定。

### 解决方案

在 SettingsViewModel 中添加 `SettingsVM => this` 属性：

```csharp
public SettingsViewModel SettingsVM => this;
```

这样 XAML 绑定 `{Binding SettingsVM.AddAiModelCommand}` 会解析到 SettingsViewModel 自身的命令。

### 未来迁移路径

1. 逐步将 XAML 绑定从 `SettingsVM.XXX` 改为 `AiModelsVM.XXX`、`ApiProvidersVM.XXX` 等
2. 删除 SettingsViewModel 中的重复方法，委托到子 ViewModel
3. 最终 SettingsViewModel 仅作为聚合器，不包含业务逻辑

---

## Phase 4: Messages 迁移详情

### 迁移前结构
```
ViewModels/Messages/                    # 旧位置（已删除）
├── FolderSelectionChangedMessage.cs
├── GlobalActionMessages.cs
├── RequestMoveFileToFolderMessage.cs
├── RequestSaveMessage.cs
└── RequestSelectFileMessage.cs
```

### 迁移后结构
```
Features/
├── Main/Messages/                      # 主窗口相关消息
│   ├── FolderSelectionChangedMessage.cs
│   ├── RequestMoveFileToFolderMessage.cs
│   ├── RequestSelectFileMessage.cs
│   └── PromptFileMessages.cs
├── Launcher/Messages/                  # 启动器相关消息
│   └── TriggerLauncherMessage.cs
├── ExternalTools/Messages/             # 外部工具相关消息
│   └── RefreshExternalToolsMessage.cs
└── Shared/Messages/                    # 共享消息
    └── SharedMessages.cs
        ├── RequestSaveMessage
        ├── RequestBackupMessage
        ├── ReloadDataMessage
        ├── ToggleWindowMessage
        ├── ToggleMainWindowMessage
        ├── OpenSettingsMessage
        ├── TriggerTranslateMessage
        ├── TriggerOcrMessage
        └── TriggerPinToScreenMessage
```

### 更新的文件
- `Features/Main/MainViewModel.cs`
- `Features/Main/MainWindow.xaml.cs`
- `Features/Main/LaunchBarWindow.xaml.cs`
- `Features/Workspace/WorkspaceViewModel.cs`
- `Features/Sidebar/SidebarViewModel.cs`
- `Features/Settings/SettingsViewModel.cs`
- `Features/Settings/SettingsWindow.xaml.cs`
- `Features/ExternalTools/ExternalToolsViewModel.cs`
- `Infrastructure/Services/GlobalShortcutCoordinator.cs`

---

## 关于 mvvm-vm-splitter Skill

项目已集成 `mvvm-vm-splitter` skill，用于拆分巨型 ViewModel。

**与 Jimmy Bogard 垂直切片的关系：**

| 方法论 | 侧重点 | 适用场景 |
|--------|--------|----------|
| Jimmy Bogard 垂直切片 | **按功能模块组织**整个 Feature | 宏观架构 |
| mvvm-vm-splitter | **拆分巨型 ViewModel** 为子 VM | 微观实现 |

**最佳实践是结合两者：**
1. 先用 **垂直切片** 划分 Feature 边界
2. 再用 **VM Splitter** 拆分 Feature 内部的巨型 ViewModel

---

*方法论来源：Jimmy Bogard - Vertical Slice Architecture*
