## 目标
- 在迷你窗口底部右下角加入两个小按钮：**AI** 与 **T**（默认颜色淡；选中时略亮但仍比输入框文字暗）。
- **T**：表示“迷你输入框作为网页对话框/直接发送”的模式。
- **AI**：表示“使用已配置 API Key 对话”的模式，并支持用前缀（默认 `ai`）触发“匹配本体提示词（patterns）”。
- 将迷你窗口当前的“思考图标”（现为 MiniSpinner）移动到 **AI 按钮左边**。

## 现状对照（当前代码已具备的能力）
- 迷你窗口已存在“AI处理中”的思考/加载动画：`MiniSpinner`（绑定 `IsAiProcessing`）。位置：[MainWindow.xaml](file:///e:/aDrive_backup/Projects/PromptMasterv5/PromptMasterv5/MainWindow.xaml#L411-L501)
- 迷你窗口已存在 AI/匹配执行方法：`ExecuteMiniAiOrPatternAsync()`（前缀命中→匹配 patterns；否则→AI Chat）。位置：[MainViewModel.ExecuteMiniAiOrPatternAsync](file:///e:/aDrive_backup/Projects/PromptMasterv5/PromptMasterv5/ViewModels/MainViewModel.cs#L442-L485)
- 迷你窗口 Enter 的分流逻辑目前由 `LocalConfig.MiniAiOnlyChatEnabled` 控制：
  - 为 `false`：直接发送（按 `LocalConfig.Mode`）
  - 为 `true`：单击 Enter → AI/匹配；双击 Enter → 坐标点击发送
  位置：[MiniInput_PreviewKeyDown](file:///e:/aDrive_backup/Projects/PromptMasterv5/PromptMasterv5/MainWindow.xaml.cs#L417-L528)

## 具体按键功能（改完后保持一致、并与你的按钮含义对齐）
- **Shift+Enter**：换行（不触发任何模式）。
- **Ctrl+Enter**：智能回退发送（SmartFocus），不管 AI/T。
- **T 模式（T亮）**：
  - Enter：按设置里的发送模式发送（SmartFocus 或 CoordinateClick）。
- **AI 模式（AI亮）**：
  - 单击 Enter（450ms 内未形成双击）：执行 AI/匹配
    - 若输入以 `ai` 前缀开头（可配置）：走“匹配本体提示词”，结果只替换输入框不自动发送
    - 否则：直接用 API Key 对话（Chat）
  - 双击 Enter：坐标点击发送到网页对话框（方便快速发送）。
- **Delete**：当当前输入框显示的是 AI 结果时，一键清空恢复输入。

## 实现步骤
### 1) 迷你窗口 UI：右下角新增按钮组 + 调整思考图标位置
- 修改 [MainWindow.xaml](file:///e:/aDrive_backup/Projects/PromptMasterv5/PromptMasterv5/MainWindow.xaml) 的 `MiniModeContainer`：
  - 在迷你输入区域底部新增一个右对齐的按钮条（思考图标 + AI + T）。
  - 将现有 `MiniSpinner` 从顶部右侧移动到按钮条中（在 AI 左侧），仍绑定 `IsAiProcessing`。
- 新增一个小按钮样式（字体小、背景透明、Hover轻微变化、选中颜色更亮）。
  - 颜色：默认淡灰；选中略亮灰；确保仍低于 `MiniTextBrush`（#DEDEDE）。

### 2) 状态与交互：AI/T 按钮切换模式并持久化
- 复用现有布尔值 `LocalConfig.MiniAiOnlyChatEnabled` 作为“AI/T 模式开关”（避免新增配置项）：
  - 点击 **AI**：将其设为 `true`（进入 AI 模式）
  - 点击 **T**：将其设为 `false`（进入 T 模式）
- 点击后立即 `LocalConfigService.Save(LocalConfig)`，确保下次打开仍保持上次模式。

### 3) 前缀与匹配：默认 `ai`，可继续在设置里改
- 将 `LocalConfig.MiniPatternPrefix` 的默认值调整为 `ai`。
- `ExecuteMiniAiOrPatternAsync()` 已支持前缀触发匹配逻辑（会去掉前缀并 TrimStart），只需保证默认值与文案一致。

### 4) 文案与可理解性
- 在迷你按钮的 ToolTip 中写清楚含义：
  - AI：API对话；`ai` 前缀→匹配提示词
  - T：作为网页输入框发送

### 5) 验证
- Release 构建。
- 手动回归：
  - AI/T 颜色变化符合“淡/略亮但不如输入框文字亮”。
  - AI 模式：单击 Enter → AI/匹配；双击 Enter → 坐标发送。
  - T 模式：Enter 直接发送（按当前设置模式）。
  - 思考图标出现在 AI 左侧，并且仅在 AI 处理中显示。

确认后我会开始修改 XAML 与 ViewModel/LocalSettings 的默认值，并完成构建验证。