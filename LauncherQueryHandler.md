分 **3 步** 把这段丑陋的磁盘 I/O 毒瘤彻底切除，转移到纯粹的 Handler 切片中。

---

### 第一步：切除“读”逻辑，创建 Query 切片 (Read Operation)

我们将把你构造函数调用的 `LoadItemOrders()` 连根拔起，提取为一个专属的读取切片。

**具体操作：**
在 `Features/Launcher/` 目录下新建一个文件夹 `Orders`（遵循“在一起”原则，排序逻辑放一块）。
新建文件 `Features/Launcher/Orders/GetLauncherOrdersQuery.cs`，并写入以下完整代码：

```csharp
using MediatR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.Orders
{
    // 1. 定义查询契约：不需要参数，返回一个字典
    public record GetLauncherOrdersQuery() : IRequest<Dictionary<string, int>>;

    // 2. 查询处理程序：把脏活累活包揽过来
    public class GetLauncherOrdersHandler : IRequestHandler<GetLauncherOrdersQuery, Dictionary<string, int>>
    {
        public Task<Dictionary<string, int>> Handle(GetLauncherOrdersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PromptMasterv6", "launcher_orders.json");

                if (File.Exists(appDataPath))
                {
                    var json = File.ReadAllText(appDataPath);
                    var result = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to load launcher orders", "GetLauncherOrdersHandler");
            }

            return Task.FromResult(new Dictionary<string, int>());
        }
    }
}

```

---

### 第二步：切除“写”逻辑，创建 Command 切片 (Write Operation)

我们在 `MoveItem` 方法里看到了极其危险的操作：每次移动 UI 元素，都在 UI 线程的同步方法里去写盘、建目录。我们要把它抽离。

**具体操作：**
在刚才的 `Features/Launcher/Orders` 文件夹下，新建文件 `SaveLauncherOrdersCommand.cs`：

```csharp
using MediatR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher.Orders
{
    // 1. 定义命令契约：拿着字典数据去保存
    public record SaveLauncherOrdersCommand(Dictionary<string, int> Orders) : IRequest;

    // 2. 命令处理程序：执行写盘动作（副作用）
    public class SaveLauncherOrdersHandler : IRequestHandler<SaveLauncherOrdersCommand>
    {
        public Task Handle(SaveLauncherOrdersCommand request, CancellationToken cancellationToken)
        {
            if (request.Orders == null) return Task.CompletedTask;

            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PromptMasterv6", "launcher_orders.json");
                
                var dir = Path.GetDirectoryName(appDataPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir!);
                }

                var json = JsonSerializer.Serialize(request.Orders);
                File.WriteAllText(appDataPath, json);
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to save launcher orders", "SaveLauncherOrdersHandler");
            }

            return Task.CompletedTask;
        }
    }
}

```

---

### 第三步：给 LauncherViewModel “抽脂” (Clean the ViewModel)

现在，回到 `Features/Launcher/LauncherViewModel.cs`。这是最爽的一步：**疯狂删代码。**

**具体操作：**

1. **删除文件顶部的这些 Using（它们再也不配出现在 VM 里了）：**
```csharp
// 删掉这三行！你的 VM 彻底告别底层 I/O
using System.IO;
using System.Text.Json;

```


2. **修改构造函数，注入 `IMediator`：**
3. **彻底删除原有的 `LoadItemOrders()` 私有方法。**
4. **重构 `InitializeItems` 和 `MoveItem`，转交控制权。**

替换为你修改后的这部分代码：

```csharp
// 【修改前的构造函数依赖】
// public LauncherViewModel(ILauncherService launcherService, ISettingsService settingsService, IWindowManager windowManager)

// 【修改后】
using MediatR; // 确保引入 MediatR
using PromptMasterv6.Features.Launcher.Orders; // 引入刚才建的切片契约

public partial class LauncherViewModel : ObservableObject
{
    private readonly ILauncherService _launcherService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowManager _windowManager;
    private readonly IMediator _mediator; // 新增中介者
    private Dictionary<string, int> _itemOrders = new();

    // ... 保持 ObservableProperty 不变 ...

    public LauncherViewModel(
        ILauncherService launcherService, 
        ISettingsService settingsService,
        IWindowManager windowManager,
        IMediator mediator) // 注入 IMediator
    {
        _launcherService = launcherService;
        _settingsService = settingsService;
        _windowManager = windowManager;
        _mediator = mediator;
        
        InitializeItemsAsync(); // 变更为 Async 调用
    }

    private async void InitializeItemsAsync()
    {
        // 降维打击：用一行代码代替之前的一大坨读文件逻辑
        _itemOrders = await _mediator.Send(new GetLauncherOrdersQuery());
        
        await LoadDiscoveredItemsAsync();
        UpdateFilter();
    }

    public async void MoveItem(LauncherItem source, LauncherItem target)
    {
        if (source == null || target == null || source == target) return;

        var oldIndex = Items.IndexOf(source);
        var newIndex = Items.IndexOf(target);

        if (oldIndex < 0 || newIndex < 0) return;

        Items.Move(oldIndex, newIndex);

        // 仅仅更新内存里的字典状态
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            item.DisplayOrder = i;
            var key = $"{item.Category}_{item.Title}";
            _itemOrders[key] = i;
        }
        
        // 降维打击：把存盘的副作用丢给后台 Handler 去做！
        await _mediator.Send(new SaveLauncherOrdersCommand(_itemOrders));

        UpdateFilter();
    }
    
    // ... 其他代码保持不变 ...
}

```

### 🔪 宗师的诊断总结

经过这 3 步：

1. `LauncherViewModel.cs` 从 160 多行缩减了约 30 行恶心的样板代码。
2. 彻底斩断了 UI 层与 `System.IO` 的强耦合，VM 重新变回了那个只懂“展示数据”和“发出命令”的纯洁对象。
3. 你的读写逻辑获得了独立的类（Handler），它们符合**单一职责原则 (SRP)**，这为以后（比如你想把配置存到云端或数据库）留下了无痛替换的后门。

