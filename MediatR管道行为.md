处理异常和打日志属于典型的**横切关注点（Cross-cutting Concerns）**。我们绝对不会在每一个 Handler 里面去写重复的 `try-catch` 和 `Log(...)`。

我们要用一招“降维打击”：**MediatR 管道行为（Pipeline Behaviors）**。这就好比给你的系统装上了一个安检门，所有进出的请求都会自动被打日志和捕获异常，而真正的业务代码（Handler）对此一无所知。

准备好手术刀，我们分 **4 步** 彻底超度这个幽灵。

---

### 第一步：铸造“安检门” —— 创建全局异常处理管道

我们将利用 MediatR 的 `IPipelineBehavior` 接口，写一个全局的拦截器。它会包裹住你所有的 Handler 执行过程。

**具体操作：**
在 `Features/Shared/` 目录下创建一个新文件夹 `Behaviors`。
新建文件 `Features/Shared/Behaviors/UnhandledExceptionBehavior.cs`，并写入以下代码：

```csharp
using MediatR;
using PromptMasterv6.Infrastructure.Services; // 引入具体的 LoggerService
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Shared.Behaviors
{
    // 拦截所有的 Request 和 Response
    public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
    {
        private readonly LoggerService _logger;

        // 【降维打击点 1】：通过标准的构造函数注入日志服务，彻底抛弃静态单例！
        public UnhandledExceptionBehavior(LoggerService logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                // 放行，让真正的 Handler 去执行业务逻辑
                return await next();
            }
            catch (Exception ex)
            {
                // 自动拦截所有 Handler 中未捕获的异常
                var requestName = typeof(TRequest).Name;
                
                // 自动记录是哪个切片（Command/Query）炸了
                _logger.LogException(ex, $"Unhandled Exception for Request {requestName}", "MediatR Pipeline");

                // 继续向外抛出（以便 UI 层做弹窗或其他处理），
                // 也可以在这里吞掉异常并返回一个包含错误信息的 Result 对象。
                throw; 
            }
        }
    }
}

```

---

### 第二步：将“安检门”安装到系统中 (DI 注册)

有了管道行为之后，你需要告诉系统的依赖注入（DI）容器，把这个管道挂载到 MediatR 上。

**具体操作：**
打开你的 `App.xaml.cs`（或者是你配置 `IServiceCollection` 的地方），找到你注册 MediatR 的代码。

```csharp
// 【修改前】可能长这样：
// services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(App).Assembly));

// 【修改后】：追加 AddOpenBehavior
using PromptMasterv6.Features.Shared.Behaviors; // 引入命名空间

services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(typeof(App).Assembly);
    
    // 【降维打击点 2】：全局挂载！以后你写的所有 Command 和 Query 都会自动经过这个异常捕获器！
    cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
});

```

---

### 第三步：大清洗 —— 撕掉 Handler 里的创可贴

现在，你的系统已经拥有了全局自动记录异常的能力。我们可以回到前面写的 `ExecuteLauncherItemHandler` 或者 `GetLauncherOrdersHandler` 中，把那些丑陋的 `try-catch` 和单例调用全部删掉！

**以你上一步写的 `ExecuteLauncherItemHandler` 为例：**

```csharp
// 【清洗前（带幽灵的恶心代码）】：
// public Task Handle(ExecuteLauncherItemCommand request, CancellationToken cancellationToken)
// {
//     try
//     {
//         // 业务逻辑...
//     }
//     catch (Exception ex)
//     {
//         // 偷偷摸摸的幽灵调用
//         Infrastructure.Services.LoggerService.Instance.LogException(ex, ...);
//     }
//     return Task.CompletedTask;
// }

// 【清洗后（极致纯洁的 VSA 切片）】：
public Task Handle(ExecuteLauncherItemCommand request, CancellationToken cancellationToken)
{
    // 如果有 Action 直接执行
    if (request.Item?.Action != null)
    {
        request.Item.Action.Invoke();
        return Task.CompletedTask;
    }

    // 启动外部程序
    if (!string.IsNullOrEmpty(request.Item?.FilePath))
    {
        var info = new ProcessStartInfo(request.Item.FilePath) { UseShellExecute = true };
        if (request.RunAsAdmin) info.Verb = "runas";
        
        // 如果这里爆炸了，抛出的异常会被 UnhandledExceptionBehavior 自动接管并记录日志！
        Process.Start(info); 
    }
    
    return Task.CompletedTask;
}

```

*看！现在的 Handler 里只剩下 100% 的纯业务逻辑，再也没有防卫性的 `try-catch` 和底层的 Logger 干扰视线。*

---

### 第四步：物理超度 —— 摧毁单例源头

只有删掉单例的定义，才能防止其他团队成员（或者未来的你）偷懒再次使用它。

**具体操作：**
打开 `Infrastructure/Services/LoggerService.cs` 文件。

找到这行代码：

```csharp
public static LoggerService Instance { get; } = new LoggerService(); // 或者是类似的静态实例实现

```

**直接选中，按 Delete 键删除它！**

从这一刻起，如果任何人在代码里打出 `LoggerService.Instance`，编译器都会毫不留情地扇他一巴掌（爆出红线报错）。任何需要打日志的地方（如果不是通过上面的 MediatR 管道），都**必须**在构造函数里老老实实地声明 `public MyClass(LoggerService logger)` 来索要依赖。

---

