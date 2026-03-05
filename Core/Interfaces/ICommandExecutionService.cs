using System.Threading.Tasks;
using PromptMasterv5.Core.Models;

namespace PromptMasterv5.Core.Interfaces
{
    public interface ICommandExecutionService : System.IDisposable
    {
        void LoadCommands();
        Task<bool> ExecuteCommandAsync(string text);
        IReadOnlyList<string> GetCommandKeys();
        System.Collections.Generic.Dictionary<string, VoiceCommand> GetCommands();
        void SetCommands(System.Collections.Generic.Dictionary<string, VoiceCommand> commands);
        void ClearIntentCache();
        event System.EventHandler CommandsChanged;

        /// <summary>
        /// Routing events to provide visual feedback during AI inference
        /// </summary>
        event System.EventHandler OnRoutingStarted;
        event System.EventHandler OnRoutingFinished;
        
        /// <summary>
        /// 尝试精确匹配指令（用于抢答模式）
        /// </summary>
        /// <param name="text">识别文本</param>
        /// <returns>匹配成功返回 true，否则返回 false</returns>
        bool TryExactMatch(string text);
    }
}
