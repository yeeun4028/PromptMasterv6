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
    }
}
