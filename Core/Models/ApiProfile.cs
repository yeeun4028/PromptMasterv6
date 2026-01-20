using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Core.Models
{
    public enum ApiProvider
    {
        Baidu = 0,
        TencentCloud = 1,
        Youdao = 2,
        AI = 3
    }

    public enum ServiceType
    {
        OCR = 0,
        Translation = 1
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

        [ObservableProperty]
        [property: JsonPropertyName("serviceType")]
        private ServiceType serviceType = ServiceType.OCR;

        // Key1 字段含义根据供应商不同：
        // 百度 OCR: API Key
        // 百度翻译: App ID
        // 腾讯云: Secret Id
        // 有道: App Key
        [ObservableProperty]
        [property: JsonPropertyName("key1")]
        private string key1 = "";

        // Key2 字段含义根据供应商不同：
        // 百度 OCR/翻译: Secret Key
        // 腾讯云: Secret Key
        // 有道: App Secret
        [ObservableProperty]
        [property: JsonPropertyName("key2")]
        private string key2 = "";

        // 显示用的字段名称（根据供应商和服务类型）
        public string Key1Label => (Provider, ServiceType) switch
        {
            (ApiProvider.Baidu, ServiceType.OCR) => "API Key",
            (ApiProvider.Baidu, ServiceType.Translation) => "App ID",
            (ApiProvider.TencentCloud, _) => "Secret Id",
            (ApiProvider.Youdao, _) => "App Key",
            _ => "Key 1"
        };

        public string Key2Label => (Provider, ServiceType) switch
        {
            (ApiProvider.Baidu, _) => "Secret Key",
            (ApiProvider.TencentCloud, _) => "Secret Key",
            (ApiProvider.Youdao, _) => "App Secret",
            _ => "Key 2"
        };

        public string ProviderDisplayName => Provider switch
        {
            ApiProvider.Baidu => "百度",
            ApiProvider.TencentCloud => "腾讯云",
            ApiProvider.Youdao => "有道",
            ApiProvider.AI => "AI",
            _ => "未知"
        };

        public string ServiceTypeDisplayName => ServiceType switch
        {
            ServiceType.OCR => "OCR",
            ServiceType.Translation => "翻译",
            _ => "未知"
        };
    }
}