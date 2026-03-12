namespace PromptMasterv6.Features.[ModuleName].[FeatureName];

public static class [Action]Feature
{
    // 1. 定义输入
    public record Command(string SomeParam); 
    
    // 2. 定义输出
    public record Result(bool Success, string Message);

    // 3. 执行逻辑
    public class Handler
    {
        // 只注入当前 Feature 绝对需要的服务
        public Handler(/* 依赖注入 */) { }

        public async Task<Result> Handle(Command request)
        {
            // 在这里实现从头到尾的业务逻辑
            return new Result(true, "成功");
        }
    }
}
4. MVVM 与 VSA 的边界
ViewModel 绝不应该包含任何具体的业务逻辑（如文件读写、云端请求）。

ViewModel 的职责仅限于：收集 UI 数据 -> 构造 Feature 的 Command -> 调用 Feature 的 Handler.Handle() -> 根据返回的 Result 更新 UI 状态（如弹窗 Toast）。