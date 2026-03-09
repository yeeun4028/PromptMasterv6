using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Core.Converters;

namespace PromptMasterv6.Features.Shared.Models;

public enum ApiProvider
{
    Baidu = 0,
    TencentCloud = 1,
    Youdao = 2,
    AI = 3,
    Google = 4,
    Tencent = 5
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
    [property: JsonPropertyName("isEnabled")]
    private bool isEnabled = true;

    [ObservableProperty]
    [property: JsonPropertyName("name")]
    private string name = "新的配置";

    [ObservableProperty]
    [property: JsonPropertyName("provider")]
    private ApiProvider provider = ApiProvider.Baidu;

    [ObservableProperty]
    [property: JsonPropertyName("serviceType")]
    private ServiceType serviceType = ServiceType.OCR;

    [ObservableProperty]
    [property: JsonPropertyName("baseUrl")]
    private string baseUrl = "";

    [ObservableProperty]
    [property: JsonPropertyName("key1")]
    [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
    private string key1 = "";

    [ObservableProperty]
    [property: JsonPropertyName("key2")]
    [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
    private string key2 = "";

    [ObservableProperty]
    [property: JsonPropertyName("useProxy")]
    private bool useProxy = false;

    public string Key1Label => (Provider, ServiceType) switch
    {
        (ApiProvider.Baidu, ServiceType.OCR) => "API Key",
        (ApiProvider.Baidu, ServiceType.Translation) => "App ID",
        (ApiProvider.TencentCloud, _) => "Secret Id",
        (ApiProvider.Youdao, _) => "App Key",
        (ApiProvider.Google, _) => "API Key",
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
        ApiProvider.Google => "Google",
        _ => "未知"
    };

    public string ServiceTypeDisplayName => ServiceType switch
    {
        ServiceType.OCR => "OCR",
        ServiceType.Translation => "翻译",
        _ => "未知"
    };
}
