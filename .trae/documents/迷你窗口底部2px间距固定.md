## 可以“完美解决”的人是谁
- 熟悉 WPF 布局系统的资深桌面端工程师（WPF/WinUI 类）：懂 Measure/Arrange、Grid/DockPanel 行为，能用布局约束替代手写高度计算
- 有 UI 细节经验的客户端工程师：会把“与窗口底部固定间距”落到容器 Padding/Margin，而不是依赖窗口高度/内容高度

## 他们的方案（本质）
- **用布局系统表达约束**：把 AI/T（含思考图标）放到单独的 Bottom 区域（Auto 行或 Dock=Bottom），并给该区域设置 Bottom Padding/Margin=2px；输入框占剩余空间（* 行或 LastChildFill）
- **不要用代码动态算高度去“追布局”**：避免 UpdateMiniHeight 这类根据文本行数/ExtentHeight 调整窗口高度的方式，因为它会引入裁剪/抖动，并破坏“固定间距”的不变量
- **固定 2px 只出现一次**：让“2px”只由一个地方负责（推荐父容器 Padding.Bottom=2），这样窗口高度变化、字体变化、按钮条高度变化时都不会把距离算重复

## 本仓库建议落地方案（最稳）
- **方案A（推荐，最简单稳）**：Grid 两行
  1) 迷你区域容器 Grid 维持 Row0='*'（输入框）、Row1='Auto'（AI/T条）
  2) 将“距迷你窗口底部 2px”放到容器的 Bottom Padding/Margin（例如把容器 Margin 改为 5,5,5,2 或外层 Border.Padding.Bottom=2）
  3) MiniModeBar 只负责与输入框间距（例如 Top=2），不再负责到底部的距离
- **方案B（同样稳）**：DockPanel
  1) DockPanel.LastChildFill=true
  2) MiniModeBar Dock=Bottom，Margin.Bottom=2
  3) TextBox 作为最后一个子元素填充剩余空间

## 需要改哪些点（实施步骤）
1) 在 MainWindow.xaml 找到迷你区域的容器（当前是 Grid.Column=0 的 Grid，Margin=5,5），把“底部固定2px”移动到该容器的 Bottom（Margin 或外层 Border.Padding）
2) 调整 MiniModeBar：设置 VerticalAlignment=Bottom，并把 Bottom=0（避免与容器 Bottom=2 叠加），仅保留与输入框的 Top 间距
3) 全局搜索是否还有 UpdateMiniHeight 或类似逻辑会在输入变化时强行改窗口高度；若有，确保它不会再影响布局（避免裁剪导致“看起来间距变了”）
4) 验证用例：
   - 迷你窗口手动拉高/拉低：AI/T（含图标）到窗口底部永远 2px
   - 输入 1 行/多行（滚动出现）时：按钮条不被挤掉，间距不变
   - 最大化/贴边/多显示器：EnsureWindowOnScreen 不破坏间距

## 验证方式
- 编译通过（Release）
- 手动验证：连续拖拽调整高度 + 输入多行 + AI 处理中显示图标，观察底部 2px 恒定