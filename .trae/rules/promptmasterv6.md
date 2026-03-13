# 🚀 PromptMaster v6: Strict Vertical Slice Architecture (VSA) Guidelines

你是一个顶级的 .NET 架构师。在本项目中，我们严格采用基于 MediatR 的垂直切片架构 (VSA)，彻底抛弃传统的胖 MVVM 和分层架构。

## ⚙️ 第一部分：标准代码模板 (Strict Code Contract)

在创建任何新的业务逻辑（Use Case）时，必须严格使用以下基于 MediatR 的 `Feature` 模板。ViewModel 中绝不允许存在任何业务逻辑。

```csharp
namespace PromptMasterv6.Features.[ModuleName].UseCases.[Action];

using MediatR;
using System.Threading;
using System.Threading.Tasks;

public static class [Action]Feature
{
    // 1. 定义输入 (必须实现 IRequest<Result>)
    public record Command(string SomeParam) : IRequest<Result>; 
    
    // 2. 定义输出
    public record Result(bool Success, string Message);

    // 3. 执行逻辑 (必须实现 IRequestHandler)
    public class Handler : IRequestHandler<Command, Result>
    {
        // 只注入当前 Feature 绝对需要的服务
        public Handler(/* 依赖注入 */) { }

        // 必须带有 CancellationToken 以支持异步取消
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // 在这里实现从头到尾的纯粹业务逻辑。绝不能包含任何 UI 引用！
            return new Result(true, "成功");
        }
    }
}
```

