# VSA 架构合规性审查报告 (v4)

**审计员**: @vsa-slice-compliance-auditor (Antigravity)
**日期**: 2026-03-13
**状态**: 部分合规 (改善中)

## 1. 总体评估
项目正在积极从传统 MVVM 迁移到 **垂直切片架构 (VSA)**。核心模块（如 `Main` 和 `Settings/Window`）已基本实现通过 MediatR/Handler 处理业务逻辑，ViewModel 职责已大幅精简。

## 2. 合规性详情

### ✅ 合规项 (Strongly Compliant)
- **Feature 对象化**: `UpdateWindowSettingsFeature` 完美符合 `Command`/`Result`/`Handler` 模式。
- **命名空间规范**: `Features.[ModuleName].[FeatureName]` 路径清晰，符合约定。
- **ViewModel 精简**: `MainViewModel` (218行) 和 `SettingsViewModel` (110行) 已成功转变为协调器角色，消除了"上帝对象"风险。

### ⚠️ 风险项 (Minor Violations / Risks)
- **ViewModel 泄漏业务逻辑**:
    - `SettingsViewModel.CloseSettings` 中直接调用 `_settingsService.SaveConfig()`。虽然简单，但严格来说保存配置属于业务行为，应封装在 Feature 中。
    - `SyncViewModel.ManualRestore` 直接调用 `_settingsService.SaveConfig()`。
- **UI 与逻辑耦合**:
    - `SyncViewModel.ManualLocalRestore` 直接 `new BackupSelectionDialog(backups)`。**严重违反 VSA 规则**: ViewModels 不应直接创建 View 实例。应转移到 `DialogService` 或通过消息驱动。
    - `MainViewModel` 仍有直接调用 `HandyControl.Controls.Growl` 的代码（L107, L111），建议统一收口到 `DialogService`。

### 🔴 违规项 (Critical Violations)
- **基础设施依赖业务 UI**:
    - `Infrastructure/Services/DialogService.cs` 内部通过硬编码 `Type.GetType` 或直接 `new IconInputDialog` 的方式引用 `Features` 或根目录下的 UI 组件。
    - **改进建议**: 保持 `Infrastructure` 纯净。复杂的业务对话框应归入对应的 `Feature/Dialogs` 文件夹，并使用抽象接口或消息机制触发。

## 3. 改进建议清单

| 优先级 | 任务 | 目标文件 |
|--------|------|----------|
| 1. P0 | 消除 `SyncViewModel` 对 `BackupSelectionDialog` 的直接依赖 | `SyncViewModel.cs` |
| 2. P1 | 修复 `SettingsViewModel.CloseSettings` 的逻辑泄漏 | `SettingsViewModel.cs` |
| 3. P1 | 统一使用 `IDialogService` 替换 `Growl` 直接调用 | `MainViewModel.cs` |
| 4. P2 | 将 `IconInputDialog` 迁移至 `Features/Shared/Dialogs/` | `IconInputDialog.xaml` |

## 4. 结论
架构重构阶段性成果显著。目前的重点应从"拆分巨型文件"转向"严格边界控制"，特别是消除 ViewModels 对具体 View 类的直接依赖。

---
*本报告由 VSA 合规审计员自动生成。*
