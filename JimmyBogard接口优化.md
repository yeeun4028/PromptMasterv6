

 `Core/Interfaces` 目录，里面简直是重灾区（如 `ISettingsService`, `IClipboardService`, `IDialogService`, `IBaiduService` 等）。

我们将按照以下 **5 个严密步骤**，把这些毫无意义的接口连根拔起。我将以 `IClipboardService` 和 `ISettingsService` 为例，为你演示完整的手术过程：

---

### 第一步：拟定“绝育名单” (The Kill List)

在动手删代码前，我们先盘点一下 `Core/Interfaces/` 下的接口，给它们判个死刑或缓刑。

**1. 准予保留的接口（真多态）：**

* `IDataService`：保留！因为你有 `WebDavDataService`（云端）和 `FileDataService`（本地）两个真实实现。
* *如果你有真正的 Mock 测试需求（比如单元测试里必须 Mock 掉外部 API），相应的接口可以保留。如果没有写单元测试，统统杀掉。*

**2. 立即处决的 1:1 接口（废接口）：**

* `ISettingsService` -> 只有 `SettingsService`
* `IClipboardService` -> 只有 `ClipboardService`
* `IDialogService` -> 只有 `DialogService`
* `IWindowManager` -> 只有 `WindowManager`
* `IContentConverterService` -> 只有 `ContentConverterService`
* `IAiService`、`IBaiduService`、`ITencentService` 等所有单体基础设施包装器。

---

### 第二步：物理抹除接口文件 (Delete the Contract)

找到你要砍掉的接口，直接按 `Delete` 键。别犹豫。

* **操作：** 在解决方案资源管理器中，直接删除以下文件（以剪贴板和转换为例）：
* 删除 `Core/Interfaces/IClipboardService.cs`
* 删除 `Core/Interfaces/IContentConverterService.cs`
* 删除 `Core/Interfaces/ISettingsService.cs`
* （以及名单上的其他废接口）



*此时你的项目会红成一片，爆出几十个编译错误。别慌，这是正常反应。*

---

### 第三步：给实现类“摘牌” (Remove Interface Implementation)

既然契约被撕毁了，实现类就不需要再挂着那块牌子了。

**操作：** 打开 `Infrastructure/Services/` 下对应的具体实现类，去掉继承声明。

**以 `ClipboardService.cs` 为例：**

```csharp
// 【修改前】
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class ClipboardService : IClipboardService
    {
        public void SetClipboard(string text) { ... }
    }
}

// 【修改后】：干掉 using 和继承
namespace PromptMasterv6.Infrastructure.Services
{
    public class ClipboardService
    {
        public void SetClipboard(string text) { ... }
    }
}

```

**以 `SettingsService.cs` 为例：**

```csharp
// 【修改前】
public class SettingsService : ISettingsService
{ ... }

// 【修改后】
public class SettingsService
{ ... }

```

---

### 第四步：修正依赖注入注册 (Fix DI Container)

由于你不再使用接口绑定，你需要告诉依赖注入（DI）容器：**“直接给我注册这个具体的类！”**

**操作：** 打开你的 `App.xaml.cs`（或者是你配置 `IServiceCollection` 的地方，比如 `Program.cs` 或 `Startup.cs`）。

```csharp
// 【修改前】：接口映射到实现
services.AddSingleton<ISettingsService, SettingsService>();
services.AddTransient<IClipboardService, ClipboardService>();
services.AddSingleton<IDialogService, DialogService>();

// 【修改后】：直接注册具体类型自身！
services.AddSingleton<SettingsService>();
services.AddTransient<ClipboardService>();
services.AddSingleton<DialogService>();

```

*技术降维点：微软自带的 DI 容器完全支持直接注入具体的类（Concrete Type），你根本不需要 Interface 也能享受依赖注入的便利！*

---

### 第五步：全局替换消费者的依赖注入 (Fix the Consumers)

这是最后一步，也是体力活。所有的 ViewModel 和 Handler 之前在构造函数里索要的是 `ISettingsService`，现在我们要让它们直接索要 `SettingsService`。

**操作：** 推荐使用 IDE（Visual Studio 或 Rider）的**“全局查找与替换” (Ctrl + Shift + F)** 功能，或者手动进入报错的 ViewModel。

**以 `Features/Main/MainViewModel.cs` 为例：**

```csharp
// 【修改前】
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IClipboardService _clipboardService;
    
    public MainViewModel(
        ISettingsService settingsService,
        IClipboardService clipboardService,
        // ... 其他依赖
        )
    {
        _settingsService = settingsService;
        _clipboardService = clipboardService;
    }
}

// 【修改后】：直接依赖具体的类
using PromptMasterv6.Infrastructure.Services; // 确保引入了具体类的命名空间

public partial class MainViewModel : ObservableObject, IDisposable
{
    // 将 I 前缀去掉！
    private readonly SettingsService _settingsService;
    private readonly ClipboardService _clipboardService;
    
    public MainViewModel(
        SettingsService settingsService,
        ClipboardService clipboardService,
        // ... 其他依赖
        )
    {
        _settingsService = settingsService;
        _clipboardService = clipboardService;
    }
}

```

**批量替换小技巧 (Global Replace)：**
你可以极其粗暴地在整个解决方案里执行替换（注意勾选“匹配大小写”和“全字匹配”）：

* 查找：`ISettingsService` -> 替换为：`SettingsService`
* 查找：`IClipboardService` -> 替换为：`ClipboardService`
* 查找：`IDialogService` -> 替换为：`DialogService`
* （替换完后，清理掉无用的 `using PromptMasterv6.Core.Interfaces;` 引用）

---

