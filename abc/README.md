# ShareX 贴图（钉图）功能实现说明

## 功能概述

贴图（PinToScreen）功能允许用户将图片固定在屏幕最前端显示，方便在工作时进行参考对照。支持缩放、透明度调节、拖拽移动等交互操作。

---

## 文件结构

```
PinToScreen/
├── PinToScreenForm.cs              # 主窗体 - 贴图显示和交互核心逻辑
├── PinToScreenForm.Designer.cs     # 主窗体设计器
├── PinToScreenForm.resx            # 主窗体资源文件
├── PinToScreenOptions.cs           # 配置选项类
├── PinToScreenOptionsForm.cs       # 选项设置窗体
├── PinToScreenOptionsForm.Designer.cs
├── PinToScreenOptionsForm.resx
├── PinToScreenStartupForm.cs       # 启动窗体 - 图片来源选择
├── PinToScreenStartupForm.Designer.cs
├── PinToScreenStartupForm.resx
└── *.xx-XX.resx                    # 多语言资源文件
```

---

## 核心类说明

### 1. PinToScreenForm - 贴图主窗体

**职责**：负责图片的显示、渲染和用户交互

#### 关键技术点

##### 1.1 窗口置顶实现
```csharp
// 通过 CreateParams 设置窗口样式，使其不显示在任务栏
protected override CreateParams CreateParams
{
    get
    {
        CreateParams createParams = base.CreateParams;
        createParams.ExStyle |= (int)WindowStyles.WS_EX_TOOLWINDOW;
        return createParams;
    }
}

// 设置窗口始终置顶
TopMost = Options.TopMost;
```

##### 1.2 多实例管理（独立线程）
```csharp
private static readonly List<PinToScreenForm> forms = new List<PinToScreenForm>();

// 每个贴图窗口在独立 STA 线程中运行
public static void PinToScreenAsync(Image image, PinToScreenOptions options, Point? location)
{
    Thread thread = new Thread(() =>
    {
        using (PinToScreenForm form = new PinToScreenForm(image, options, location))
        {
            forms.Add(form);
            form.ShowDialog();
            forms.Remove(form);
        }
    });
    thread.IsBackground = true;
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
}
```

##### 1.3 图片缩放与透明度
```csharp
// 缩放范围：20% - 500%
public int ImageScale
{
    set
    {
        int newImageScale = value.Clamp(20, 500);
        if (imageScale != newImageScale)
        {
            imageScale = newImageScale;
            AutoSizeForm();  // 自动调整窗口大小
        }
    }
}

// 透明度范围：10% - 100%
public int ImageOpacity
{
    set
    {
        int newImageOpacity = value.Clamp(10, 100);
        imageOpacity = newImageOpacity;
        Opacity = imageOpacity / 100f;  // 设置窗体透明度
    }
}
```

##### 1.4 自定义绘制（OnPaint）
```csharp
protected override void OnPaint(PaintEventArgs e)
{
    Graphics g = e.Graphics;
    g.Clear(Options.BackgroundColor);

    // 绘制边框
    if (Options.Border)
    {
        using (Pen pen = new Pen(Options.BorderColor, Options.BorderSize))
        {
            g.DrawRectangleProper(pen, new Rectangle(position, FormSize));
        }
    }

    // 根据缩放比例选择插值模式
    if (Options.HighQualityScale)
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    else
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

    // 绘制图片
    g.DrawImage(Image, new Rectangle(position, ImageSize), ...);
}
```

##### 1.5 窗口阴影效果（DWM）
```csharp
protected override void WndProc(ref Message m)
{
    if (Options.Shadow && m.Msg == (int)WindowsMessages.NCPAINT)
    {
        // 启用 DWM 窗口阴影
        NativeMethods.SetNCRenderingPolicy(Handle, DWMNCRENDERINGPOLICY.DWMNCRP_ENABLED);

        // Windows 11 圆角处理
        if (Helpers.IsWindows11OrGreater())
        {
            NativeMethods.SetWindowCornerPreference(Handle, DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND);
        }

        // 扩展窗口边框以显示阴影
        NativeMethods.DwmExtendFrameIntoClientArea(Handle, ref margins);
    }
    base.WndProc(ref m);
}
```

---

### 2. PinToScreenOptions - 配置选项类

**职责**：存储贴图功能的所有可配置参数

```csharp
public class PinToScreenOptions
{
    public int InitialScale { get; set; } = 100;        // 初始缩放比例
    public int ScaleStep { get; set; } = 10;            // 缩放步进值
    public bool HighQualityScale { get; set; } = true;  // 高质量缩放
    public int InitialOpacity { get; set; } = 100;      // 初始透明度
    public int OpacityStep { get; set; } = 10;          // 透明度步进值
    public ContentAlignment Placement { get; set; }     // 初始位置
    public int PlacementOffset { get; set; } = 10;      // 位置偏移
    public bool TopMost { get; set; } = true;           // 始终置顶
    public bool KeepCenterLocation { get; set; } = true;// 缩放时保持中心
    public Color BackgroundColor { get; set; } = Color.White;
    public bool Shadow { get; set; } = true;            // 窗口阴影
    public bool Border { get; set; } = true;            // 显示边框
    public int BorderSize { get; set; } = 2;            // 边框宽度
    public Color BorderColor { get; set; } = Color.CornflowerBlue;
    public Size MinimizeSize { get; set; } = new Size(100, 100);  // 最小化尺寸
}
```

---

### 3. PinToScreenStartupForm - 启动窗体

**职责**：提供图片来源选择界面

#### 支持的图片来源

| 来源 | 方法 | 说明 |
|------|------|------|
| 屏幕截图 | `btnFromScreen_Click` | 调用 `RegionCaptureTasks.GetRegionImage()` 进行区域截图 |
| 剪贴板 | `btnFromClipboard_Click` | 从剪贴板获取图片 `ClipboardHelpers.TryGetImage()` |
| 文件 | `btnFromFile_Click` | 通过文件对话框选择图片 |

---

### 4. PinToScreenOptionsForm - 选项设置窗体

**职责**：提供图形化界面配置贴图参数

#### 可配置项
- 初始位置（9个方位）
- 位置偏移量
- 始终置顶
- 缩放时保持中心位置
- 窗口阴影
- 边框（开关、宽度、颜色）
- 最小化尺寸
- 缩放步进值

---

## 用户交互操作

### 鼠标操作

| 操作 | 功能 |
|------|------|
| 左键拖拽 | 移动窗口 |
| 左键双击 | 切换最小化状态 |
| 右键点击 | 关闭窗口 |
| 中键点击 | 重置缩放和透明度 |
| 滚轮 | 调整缩放比例 |
| Ctrl + 滚轮 | 调整透明度 |

### 键盘操作

| 按键 | 功能 |
|------|------|
| 方向键 | 移动窗口（1像素） |
| Shift + 方向键 | 快速移动窗口（10像素） |
| Ctrl + C | 复制图片到剪贴板 |
| +/- | 调整缩放比例 |
| Ctrl + +/- | 调整透明度 |

---

## 技术要点总结

### 1. 窗口特性
- 使用 `WS_EX_TOOLWINDOW` 样式，不显示在任务栏和 Alt+Tab 切换列表
- `TopMost = true` 实现始终置顶
- 通过 `Opacity` 属性实现窗口透明度

### 2. 多线程架构
- 每个贴图窗口运行在独立的 STA 线程中
- 使用静态列表 `forms` 管理所有实例
- 线程安全：使用 `lock (syncLock)` 保护共享资源

### 3. 图像处理
- 使用 GDI+ 进行图像绘制
- 支持高质量双三次插值或最近邻插值
- 自定义 `OnPaint` 实现完全控制绘制过程

### 4. Windows API 调用
- `SetWindowPos` - 精确控制窗口位置和大小
- `DwmExtendFrameIntoClientArea` - DWM 阴影效果
- `SetWindowCornerPreference` - Windows 11 圆角控制

### 5. 用户体验优化
- 自定义手型光标（张开/握紧）
- 鼠标进入时显示工具栏，离开时隐藏
- 缩放时可选保持中心位置不变

---

## 调用方式

```csharp
// 从图片对象创建贴图
PinToScreenForm.PinToScreenAsync(image, options, location);

// 关闭所有贴图窗口
PinToScreenForm.CloseAll();
```

---

## 依赖项

- `ShareX.HelpersLib` - 辅助工具类（剪贴板、光标、Windows API）
- `ShareX.ScreenCaptureLib` - 屏幕截图功能
- `System.Drawing` - GDI+ 图像处理
- `System.Windows.Forms` - Windows Forms 框架
