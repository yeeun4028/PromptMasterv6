using PromptMasterv6.Core.Models;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IVariableService
    {
        void ParseVariables(string content, ObservableCollection<VariableItem> variables);
        string CompileContent(string? content, ObservableCollection<VariableItem> variables, string? additionalInput = null);
        bool HasVariables(ObservableCollection<VariableItem> variables);
    }
}
