namespace PromptMasterv6.Features.AiModels.Shared;

public enum ConnectionTestStatus
{
    Idle,
    Testing,
    Success,
    Failed
}

public enum TranslationTestStatus
{
    Idle,
    Testing,
    FullSuccess,
    PartialSuccess,
    Failed
}
