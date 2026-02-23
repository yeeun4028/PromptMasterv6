namespace PromptMasterv5.Core.Interfaces
{
    public interface ICommandExecutionService : System.IDisposable
    {
        void LoadCommands();
        bool ExecuteCommand(string text);
        IReadOnlyList<string> GetCommandKeys();
        System.Collections.Generic.Dictionary<string, string> GetCommands();
        void SetCommands(System.Collections.Generic.Dictionary<string, string> commands);
        event System.EventHandler CommandsChanged;
        
        /// <summary>
        /// 尝试精确匹配指令（用于抢答模式）
        /// </summary>
        /// <param name="text">识别文本</param>
        /// <returns>匹配成功返回 true，否则返回 false</returns>
        bool TryExactMatch(string text);
    }
}
