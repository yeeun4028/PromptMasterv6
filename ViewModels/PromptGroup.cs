
using System.Collections.Generic;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.ViewModels
{
    public class PromptGroup
    {
        public string FolderName { get; set; } = "";
        public IEnumerable<PromptItem> Prompts { get; set; } = new List<PromptItem>();
    }
}
