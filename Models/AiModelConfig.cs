using System.Text.Json.Serialization;

namespace PromptMasterv5.Models
{
    public class AiModelConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = "";

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = "";

        [JsonPropertyName("modelName")]
        public string ModelName { get; set; } = "";
    }
}
