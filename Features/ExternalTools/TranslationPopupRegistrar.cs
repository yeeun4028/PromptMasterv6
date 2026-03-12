using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Infrastructure.WindowRegistration;

namespace PromptMasterv6.Features.ExternalTools
{
    /// <summary>
    /// 翻译弹窗窗口注册器
    /// </summary>
    public class TranslationPopupRegistrar : IWindowRegistrar
    {
        public void Register(WindowRegistry registry)
        {
            registry.RegisterTranslationPopup(text =>
                new TranslationPopup(text));
        }
    }
}
