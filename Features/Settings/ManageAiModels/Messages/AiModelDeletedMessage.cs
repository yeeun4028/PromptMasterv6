using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.AiModels.Messages
{
    public class AiModelDeletedMessage
    {
        public string DeletedModelId { get; }
        public string DeletedModelName { get; }

        public AiModelDeletedMessage(AiModelConfig deletedModel)
        {
            DeletedModelId = deletedModel.Id;
            DeletedModelName = deletedModel.DisplayName ?? deletedModel.ModelName;
        }
    }
}
