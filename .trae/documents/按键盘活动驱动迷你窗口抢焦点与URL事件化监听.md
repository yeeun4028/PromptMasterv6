## 已确认前提
- A：用全局键盘 KeyDown 作为触发（优先级最高，兼容中文 IME）。
- B：命中时允许 Activate/SetForegroundWindow，把迷你窗口切为前台并聚焦 MiniInputBox。

## 实施内容
### 1) 全局键盘触发（事件驱动，无轮询）
- 扩展 GlobalKeyService：新增 OnAnyKeyDown 事件（不拦截输入，只通知），在 Hook.GlobalEvents() 上订阅 KeyDown。

### 2) 地址栏 URL 读取（只限 Chrome/Edge，且只读地址栏）
- 复用/抽取当前项目里的 UIAutomation 能力，但改为“地址栏专用定位”：
  - 仅当前台进程为 chrome/msedge 才尝试。
  - 在 AutomationElement 树中筛选 Edit 控件：
    - 位置/尺寸过滤（靠近窗口上方、足够宽）
    - Name/AutomationId 含 Address/Search/地址/网址/搜索 等提示优先
    - Value 必须像 URL（含 :// 或域名/特殊 scheme），且不含空白
  - 只取评分最高的那个作为地址栏 URL。

### 3) 命中关键字即抢焦点
- 在 MainWindow 订阅 GlobalKeyService.OnAnyKeyDown：
  - KeyDown 发生时立即读取前台地址栏 URL。
  - 从 LocalConfig.CoordinateRules[].UrlContains 取关键字，按连续子串 Contains(OrdinalIgnoreCase) 匹配。
  - 命中：
    - 若迷你窗口隐藏则 Show
    - 调用 BringToFrontAndEnsureOnScreen()（Activate/Topmost/SetForegroundWindow）
    - Dispatcher 聚焦 MiniInputBox（Caret 到末尾）
  - 不命中：不做任何事（不拉起迷你窗口）。

### 4) 移除现有轮询与鼠标监测路径
- 删除 MainWindow 内的 URL 轮询 DispatcherTimer、全局鼠标 Hook，以及相关状态变量。
- （可选）保留失焦隐藏逻辑不变。

### 5) 验证
- Chrome/Edge 打开命中关键字页面：不点击迷你窗口，直接开始中文/英文输入，确认输入进入 MiniInputBox。
- 切换到不命中页面：按键不应拉起迷你窗口。
- 检查不会再出现“所有网页都被误判命中”。

## 影响范围
- 修改文件：GlobalKeyService.cs、MainWindow.xaml.cs（可能抽出一个 UrlDetector 辅助类以便复用）。
- 不新增 NuGet 依赖。