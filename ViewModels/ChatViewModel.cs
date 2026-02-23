using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Helpers;
using PromptMasterv5.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PromptMasterv5.ViewModels.Messages;

namespace PromptMasterv5.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private readonly IAiService _aiService;
        private readonly FabricService _fabricService;

        public Func<AppConfig>? ConfigProvider { get; set; }
        public Func<LocalSettings>? LocalConfigProvider { get; set; }
        public Func<IEnumerable<PromptItem>>? FilesProvider { get; set; }

        [ObservableProperty]
        private bool isAiProcessing = false;

        [ObservableProperty]
        private bool isAiResultDisplayed = false;

        public ChatViewModel(IAiService aiService, FabricService fabricService)
        {
            _aiService = aiService;
            _fabricService = fabricService;
        }

        private static string NormalizeSymbols(string s) => StringUtils.NormalizeSymbols(s);
    }
}
