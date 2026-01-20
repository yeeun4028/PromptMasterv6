namespace PromptMasterv5.Core.Interfaces
{
    public interface IWindowManager
    {
        byte[]? ShowCaptureWindow();
        void ShowTranslationPopup(string text);
        // Can be extended for other windows later
    }
}
