

如果把 `Process.Start` 留在 ViewModel 里，你的代码不仅无法写单元测试，还会让 ViewModel 彻底沦为一个无所不包的“上帝类”。

准备好手术刀，我们分 **2 步** 把它切下来，装进它该待的试管里。

---

### 第一步：提取“执行”切片 (The Execution Slice)

我们将创建一个专门负责“启动进程”的 Command 和 Handler。这个 Handler 将作为唯一的“合法处刑人”，全权接管与底层操作系统（`System.Diagnostics`）的交互，并把那个偷偷摸摸的异常日志记录（`LoggerService.Instance`）也一并带走。

**具体操作：**
在 `Features/Launcher/` 目录下新建一个文件夹 `Execution`。
新建文件 `Features/Launcher/Execution/ExecuteLauncherItemCommand.cs`，并填入以下完整代码：

```csharp
using MediatR;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
// 注意：如果上一步你已经把 LauncherItem 移过来了，确保命名空间正确
// using PromptMasterv6.Features.Launcher; 

namespace PromptMasterv6.Features.Launcher.Execution
{
    // 1. 定义命令契约：我需要什么才能执行？(需要那个 Item，以及是否需要管理员权限)
    public record ExecuteLauncherItemCommand(LauncherItem Item, bool RunAsAdmin) : IRequest;

    // 2. 命令处理程序：真正的“处刑人”
    public class ExecuteLauncherItemHandler : IRequestHandler<ExecuteLauncherItemCommand>
    {
        public Task Handle(ExecuteLauncherItemCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 场景 A：执行内置的委托动作
                if (request.Item?.Action != null)
                {
                    request.Item.Action.Invoke();
                    return Task.CompletedTask;
                }

                // 场景 B：拉起外部文件/程序
                if (!string.IsNullOrEmpty(request.Item?.FilePath))
                {
                    var info = new ProcessStartInfo(request.Item.FilePath) 
                    { 
                        UseShellExecute = true 
                    };
                    
                    if (request.RunAsAdmin)
                    {
                        info.Verb = "runas";
                    }

                    // 【降维打击点】：整个系统只有这里允许调用底层 OS 进程 API
                    Process.Start(info);
                }
            }
            catch (Exception ex)
            {
                // 日志记录这个副作用也从 ViewModel 转移到了这里
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Failed to execute launcher item", "ExecuteLauncherItemHandler");
            }
            
            return Task.CompletedTask;
        }
    }
}

```

---

### 第二步：净化 ViewModel，移交兵权 (Cleanse the ViewModel)

现在，“处刑人”已经就位了。我们回到 `Features/Launcher/LauncherViewModel.cs`，剥夺它直接操作系统的权力。

**具体操作：**

1. **删除头部引用：**
直接删掉这行代码，让 ViewModel 彻底与系统进程断绝关系：
```csharp
using System.Diagnostics; // 删掉它！

```


2. **重写 `ExecuteItem` 方法：**
找到原来的 `ExecuteItem` 方法（连同它臃肿的 `try-catch` 块），用下面这几行极其清爽的代码替换它：

```csharp
// 【修改前的恶心代码】：
// [RelayCommand]
// private void ExecuteItem(LauncherItem item)
// {
//     try { var info = new ProcessStartInfo... Process.Start(info); ... } catch { LoggerService... }
// }

// 【修改后的清爽代码】：
using PromptMasterv6.Features.Launcher.Execution; // 确保引入了新切片的命名空间

[RelayCommand]
private async Task ExecuteItem(LauncherItem item)
{
    if (item == null) return;

    // 把执行的脏活儿丢给 MediatR 派发，VM 只负责传递数据和状态
    await _mediator.Send(new ExecuteLauncherItemCommand(item, Config.LauncherRunAsAdmin));
    
    // UI 状态反馈：请求关闭窗口
    RequestClose?.Invoke();
}

```

