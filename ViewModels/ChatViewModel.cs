using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Helpers;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PromptMasterv6.ViewModels.Messages;

namespace PromptMasterv6.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private readonly IAiService _aiService;

        public Func<AppConfig>? ConfigProvider { get; set; }
        public Func<LocalSettings>? LocalConfigProvider { get; set; }
        public Func<IEnumerable<PromptItem>>? FilesProvider { get; set; }

        [ObservableProperty]
        private bool isAiProcessing = false;

        [ObservableProperty]
        private bool isAiResultDisplayed = false;

        public ChatViewModel(IAiService aiService)
        {
            _aiService = aiService;
        }

        private static string NormalizeSymbols(string s) => StringUtils.NormalizeSymbols(s);
    }
}
