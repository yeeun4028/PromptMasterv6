using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv6.Features.Shared.Models;

public partial class AiModelConfig : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("id")]
    private string id = Guid.NewGuid().ToString();

    [ObservableProperty]
    [property: JsonPropertyName("baseUrl")]
    private string baseUrl = "";

    [ObservableProperty]
    [property: JsonPropertyName("apiKey")]
    private string apiKey = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [property: JsonPropertyName("modelName")]
    private string modelName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [property: JsonPropertyName("remark")]
    private string remark = "";

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Remark) ? ModelName : Remark;

    [ObservableProperty]
    [property: JsonPropertyName("isEnableForTranslation")]
    private bool isEnableForTranslation;

    [ObservableProperty]
    [property: JsonPropertyName("isEnableForOcr")]
    private bool isEnableForOcr;

    [ObservableProperty]
    [property: JsonPropertyName("isEnableForScreenshotTranslate")]
    private bool isEnableForScreenshotTranslate;

    [ObservableProperty]
    [property: JsonPropertyName("useProxy")]
    private bool useProxy;

    [JsonIgnore]
    [ObservableProperty]
    private bool isPendingDelete;
}
