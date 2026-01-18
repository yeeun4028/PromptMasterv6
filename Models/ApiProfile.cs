using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Models
{
    public enum ApiProvider
    {
        Baidu = 0
    }

    public partial class ApiProfile : ObservableObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty]
        [property: JsonPropertyName("name")]
        private string name = "新的配置";

        [ObservableProperty]
        [property: JsonPropertyName("provider")]
        private ApiProvider provider = ApiProvider.Baidu;

        // 对于百度翻译：AppId
        // 对于百度OCR：API Key
        [ObservableProperty]
        [property: JsonPropertyName("key1")]
        private string key1 = "";

        // 对于百度翻译：Secret Key
        // 对于百度OCR：Secret Key
        [ObservableProperty]
        [property: JsonPropertyName("key2")]
        private string key2 = "";
    }
}