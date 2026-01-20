using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Core.Models
{
    /// <summary>
    /// AI 翻译提示词配置（收藏）
    /// </summary>
    public partial class AiTranslationConfig : ObservableObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty]
        [property: JsonPropertyName("promptId")]
        private string promptId = "";

        [ObservableProperty]
        [property: JsonPropertyName("promptTitle")]
        private string promptTitle = "";

        [ObservableProperty]
        [property: JsonPropertyName("baseUrl")]
        private string baseUrl = "";

        [ObservableProperty]
        [property: JsonPropertyName("apiKey")]
        private string apiKey = "";

        [ObservableProperty]
        [property: JsonPropertyName("model")]
        private string model = "";

        public string DisplayName => string.IsNullOrWhiteSpace(PromptTitle) 
            ? $"{Model}" 
            : $"{PromptTitle} - {Model}";
    }
}
