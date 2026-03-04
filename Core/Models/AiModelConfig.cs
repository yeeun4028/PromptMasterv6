using System.Text.Json.Serialization;

namespace PromptMasterv5.Core.Models
{
    public partial class AiModelConfig : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: JsonPropertyName("id")]
        private string id = Guid.NewGuid().ToString();

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: JsonPropertyName("baseUrl")]
        private string baseUrl = "";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: JsonPropertyName("apiKey")]
        private string apiKey = "";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(DisplayName))]
        [property: JsonPropertyName("modelName")]
        private string modelName = "";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(DisplayName))]
        [property: JsonPropertyName("remark")]
        private string remark = "";

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Remark) ? ModelName : Remark;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: JsonPropertyName("isEnableForTranslation")]
        private bool isEnableForTranslation;


        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: JsonPropertyName("isEnableForOcr")]
        private bool isEnableForOcr;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: JsonPropertyName("isEnableForScreenshotTranslate")]
        private bool isEnableForScreenshotTranslate;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: JsonPropertyName("useProxy")]
        private bool useProxy;

        [JsonIgnore]
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool isPendingDelete;
    }
}
