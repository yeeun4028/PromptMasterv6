## 已确认的关键规则
- AI 查询开关开启时：
  - 无前缀：Enter → 纯聊天（不走 patterns）
  - 有前缀（默认 `''`）：Enter → patterns 匹配，生成 assembled prompt 并**只替换输入框，不自动发送**
  - Ctrl+Enter / 双击 Enter：始终发送到网页输入框（智能回退/坐标点击）
- AI 查询开关未启时：
  - 迷你输入框当作网页输入框：Enter 默认发送
  - Enter 的默认发送方式：按 `LocalConfig.Mode`（智能回退/坐标点击）
  - Shift+Enter：换行

## Step 1：扩展 LocalSettings（持久化）
- 新增字段：
  - `MiniAiOnlyChatEnabled`（bool）
  - `MiniPatternPrefix`（string，默认 `''`，允许重复同符号）
  - `MiniAlwaysOnTopHotkeyPrefix`（string，允许重复同符号，用于强制置顶）
- 新增运行态字段（不落盘）：
  - `IsMiniTopmostLocked`（bool，用于置顶锁定与防自动隐藏）

## Step 2：设置页 UI（极简窗口设置）
- 新增三行：
  1) 强制窗口置顶｜右侧：符号快捷键输入框（可输入多个相同符号）
  2) AI查询（默认聊天；前缀触发提示词匹配）｜右侧：开关
  3) 提示词匹配｜右侧：前缀输入框（默认 `''`）
- 增加说明区：
  - Enter =（AI开：聊天/匹配；AI关：发送）
  - Shift+Enter = 换行
  - Ctrl+Enter/双击 Enter = 发送到网页输入框

## Step 3：全局符号热键识别（强制置顶）
- 扩展现有全局键盘 Hook（当前已有“双击分号/双击Ctrl”）：
  - 改为检测用户配置的符号序列（例如 `''''`）
  - 做中英文符号归一化（例如 `；`≈`;`，中文/全角引号归一到 `'`），保证“不分中英文”
- 触发后：显示极简窗口并设置 `IsMiniTopmostLocked=true`、`Topmost=true`

## Step 4：迷你输入框按键分流实现
- Shift+Enter：换行（不触发分流）
- Ctrl+Enter：发送到网页输入框（强制智能回退）
- 双击 Enter：发送到网页输入框（强制坐标点击）
- 单 Enter：
  - 若 `MiniAiOnlyChatEnabled=true`：
    - 以 `MiniPatternPrefix` 开头 → patterns 匹配并替换输入框
    - 否则 → 纯聊天并替换输入框
  - 若 `MiniAiOnlyChatEnabled=false`：
    - 发送到网页输入框，发送方式按 `LocalConfig.Mode`

## Step 5：patterns 匹配实现
- 复用 `FabricService`：选择最佳 pattern 并读取 `system.md`
- 组装 assembled prompt：`{patternContent}\n\n---\n\nUSER INPUT:\n{query}`
- 将 assembled prompt 写回迷你输入框（不自动发送）

## Step 6：置顶锁定与自动隐藏兼容
- 当前“失焦取消置顶+自动 Hide”逻辑：当 `IsMiniTopmostLocked=true` 时跳过
- ESC/退出极简时清锁定并恢复原行为

## Step 7：验证
- Release 构建
- 回归：AI 开/关 + 前缀/非前缀 + Shift/Ctrl/双击 Enter + 置顶快捷键中文/英文符号

确认无误后，我将按以上 Step 1→7 顺序逐步落地实现。