using System;
using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IWindowManager
    {
        byte[]? ShowCaptureWindow(Func<byte[], Task>? onCaptureProcessing = null);
        void ShowTranslationPopup(string text);
        // Can be extended for other windows later
    }
}
