using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
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

        public ObservableCollection<PromptItem> MiniPinnedPrompts { get; } = new();

        [ObservableProperty]
        private string miniInputText = "";

        [ObservableProperty]
        private ObservableCollection<PromptItem> searchResults = new();

        [ObservableProperty]
        private PromptItem? selectedSearchItem;

        [ObservableProperty]
        private bool isAiProcessing = false;

        [ObservableProperty]
        private bool isAiResultDisplayed = false;

        [ObservableProperty]
        private bool isSearchPopupOpen = false;

        public ChatViewModel(IAiService aiService, FabricService fabricService)
        {
            _aiService = aiService;
            _fabricService = fabricService;
        }

        partial void OnMiniInputTextChanged(string value)
        {
            WeakReferenceMessenger.Default.Send(new MiniInputTextChangedMessage(value));
        }

        public async Task ExecuteAiQuery()
        {
            var config = ConfigProvider?.Invoke();
            var localConfig = LocalConfigProvider?.Invoke();
            if (config == null || localConfig == null) return;

            string inputText = MiniInputText.Trim();
            string query;

            bool needPrefix = !localConfig.MiniWindowUseAi;

            if (needPrefix)
            {
                if (inputText.StartsWith("ai ", StringComparison.OrdinalIgnoreCase))
                    query = inputText.Substring(3);
                else if (inputText.StartsWith("ai　", StringComparison.OrdinalIgnoreCase))
                    query = inputText.Substring(3);
                else if (inputText.StartsWith("''"))
                    query = inputText.Substring(2);
                else
                    return;
            }
            else
            {
                query = inputText;
            }

            if (string.IsNullOrWhiteSpace(query)) return;

            IsAiProcessing = true;
            try
            {
                string patternContent = await _fabricService.FindBestPatternAndContentAsync(query, _aiService, config);
                if (!string.IsNullOrEmpty(patternContent))
                {
                    string assembledPrompt = $"{patternContent}\n\n---\n\nUSER INPUT:\n{query}";
                    MiniInputText = assembledPrompt;
                    IsAiResultDisplayed = true;
                }
                else
                {
                    string apiKey = config.AiApiKey;
                    string baseUrl = config.AiBaseUrl;
                    string modelId = config.AiModel;

                    if (!string.IsNullOrWhiteSpace(config.MiniWindowModelId))
                    {
                        var model = config.SavedModels.FirstOrDefault(m => m.Id == config.MiniWindowModelId);
                        if (model != null)
                        {
                            apiKey = model.ApiKey;
                            baseUrl = model.BaseUrl;
                            modelId = model.ModelName;
                        }
                    }

                    string result = await _aiService.ChatAsync(query, apiKey, baseUrl, modelId);
                    MiniInputText = result;
                    IsAiResultDisplayed = true;
                }
            }
            catch (Exception ex)
            {
                MiniInputText = $"[AI 错误] {ex.Message}";
                IsAiResultDisplayed = true;
            }
            finally
            {
                IsAiProcessing = false;
            }
        }

        private static char NormalizeSymbol(char c)
        {
            return c switch
            {
                '；' => ';',
                '＇' => '\'',
                '‘' => '\'',
                '’' => '\'',
                '`' => '\'',
                '´' => '\'',
                _ => c
            };
        }

        private static string NormalizeSymbols(string s)
        {
            return new string((s ?? "").Select(NormalizeSymbol).ToArray());
        }

        public async Task ExecuteMiniAiOrPatternAsync()
        {
            var config = ConfigProvider?.Invoke();
            var localConfig = LocalConfigProvider?.Invoke();
            if (config == null || localConfig == null) return;

            var inputText = MiniInputText.Trim();
            if (string.IsNullOrWhiteSpace(inputText)) return;

            IsAiProcessing = true;
            try
            {
                var prefix = localConfig.MiniPatternPrefix ?? "";
                var normalizedInput = NormalizeSymbols(inputText);
                var normalizedPrefix = NormalizeSymbols(prefix);

                if (!string.IsNullOrWhiteSpace(prefix) && normalizedInput.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                {
                    var query = inputText.Substring(prefix.Length).TrimStart();
                    if (string.IsNullOrWhiteSpace(query)) return;

                    var patternContent = await _fabricService.FindBestPatternAndContentAsync(query, _aiService, config);
                    if (!string.IsNullOrEmpty(patternContent))
                    {
                        MiniInputText = $"{patternContent}\n\n---\n\nUSER INPUT:\n{query}";
                    }
                    else
                    {
                        MiniInputText = $"[提示词匹配失败] {query}";
                    }
                    IsAiResultDisplayed = true;
                    return;
                }

                string apiKey = config.AiApiKey;
                string baseUrl = config.AiBaseUrl;
                string modelId = config.AiModel;

                if (!string.IsNullOrWhiteSpace(config.MiniWindowModelId))
                {
                    var model = config.SavedModels.FirstOrDefault(m => m.Id == config.MiniWindowModelId);
                    if (model != null)
                    {
                        apiKey = model.ApiKey;
                        baseUrl = model.BaseUrl;
                        modelId = model.ModelName;
                    }
                }

                var result = await _aiService.ChatAsync(inputText, apiKey, baseUrl, modelId);
                MiniInputText = result;
                IsAiResultDisplayed = true;
            }
            catch (Exception ex)
            {
                MiniInputText = $"[AI 错误] {ex.Message}";
                IsAiResultDisplayed = true;
            }
            finally
            {
                IsAiProcessing = false;
            }
        }

        private void PerformSearch(string keyword)
        {
            var files = FilesProvider?.Invoke();
            if (files == null) return;

            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                foreach (var file in files.Take(10)) SearchResults.Add(file);
            }
            else
            {
                var lowerKey = keyword.ToLowerInvariant();
                var matches = files.Where(f => (f.Title ?? "").ToLowerInvariant().Contains(lowerKey)).Take(10);
                foreach (var m in matches) SearchResults.Add(m);
            }

            if (SearchResults.Count > 0) SelectedSearchItem = SearchResults.FirstOrDefault();
        }

        public void UpdateSearchPopup(string keyword)
        {
            PerformSearch(keyword);
            IsSearchPopupOpen = SearchResults.Count > 0;
        }

        [RelayCommand]
        private void SelectNextSearchResult()
        {
            if (SearchResults.Count <= 0) return;

            var currentIndex = SelectedSearchItem == null ? -1 : SearchResults.IndexOf(SelectedSearchItem);
            var nextIndex = currentIndex + 1;
            if (nextIndex >= SearchResults.Count) nextIndex = 0;
            SelectedSearchItem = SearchResults[nextIndex];
        }

        [RelayCommand]
        private void SelectPrevSearchResult()
        {
            if (SearchResults.Count <= 0) return;

            var currentIndex = SelectedSearchItem == null ? 0 : SearchResults.IndexOf(SelectedSearchItem);
            var prevIndex = currentIndex - 1;
            if (prevIndex < 0) prevIndex = SearchResults.Count - 1;
            SelectedSearchItem = SearchResults[prevIndex];
        }

        [RelayCommand]
        private void ConfirmSearchResult()
        {
            if (SelectedSearchItem != null)
            {
                MiniInputText = SelectedSearchItem.Content ?? "";
                IsSearchPopupOpen = false;
            }
        }

        public void SyncMiniPinnedPrompts(ObservableCollection<string> pinnedIds, string selectedPinnedId)
        {
            var files = FilesProvider?.Invoke();
            if (files == null) return;

            var validIds = pinnedIds.Where(id => files.Any(f => f.Id == id)).ToList();
            if (validIds.Count != pinnedIds.Count)
            {
                pinnedIds.Clear();
                foreach (var id in validIds) pinnedIds.Add(id);
            }

            MiniPinnedPrompts.Clear();
            foreach (var id in pinnedIds)
            {
                var p = files.FirstOrDefault(f => f.Id == id);
                if (p != null) MiniPinnedPrompts.Add(p);
            }
        }

        public void AddMiniPinnedPromptFromCandidate(ObservableCollection<string> pinnedIds, string candidateId)
        {
            if (string.IsNullOrWhiteSpace(candidateId)) return;
            if (pinnedIds.Contains(candidateId)) return;

            var files = FilesProvider?.Invoke();
            if (files == null) return;

            var prompt = files.FirstOrDefault(f => f.Id == candidateId);
            if (prompt == null) return;

            pinnedIds.Add(candidateId);
            MiniPinnedPrompts.Add(prompt);
        }

        public void RemoveMiniPinnedPromptById(ObservableCollection<string> pinnedIds, Action clearSelectedPinnedIfMatch, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            for (int i = MiniPinnedPrompts.Count - 1; i >= 0; i--)
            {
                if (MiniPinnedPrompts[i].Id == id) MiniPinnedPrompts.RemoveAt(i);
            }

            for (int i = pinnedIds.Count - 1; i >= 0; i--)
            {
                if (pinnedIds[i] == id) pinnedIds.RemoveAt(i);
            }

            clearSelectedPinnedIfMatch();
        }

        public void ReorderMiniPinnedPrompts(ObservableCollection<string> pinnedIds, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= MiniPinnedPrompts.Count) return;
            if (newIndex < 0 || newIndex >= MiniPinnedPrompts.Count) return;
            if (oldIndex == newIndex) return;

            var moved = MiniPinnedPrompts[oldIndex];
            MiniPinnedPrompts.Move(oldIndex, newIndex);

            var id = moved.Id;
            var oldIdIndex = pinnedIds.IndexOf(id);
            if (oldIdIndex < 0) return;
            pinnedIds.RemoveAt(oldIdIndex);
            pinnedIds.Insert(newIndex, id);
        }
    }
}
