namespace PromptMasterv6.Core.Interfaces
{
    public interface IClipboardService
    {
        void SetClipboard(string text);
        void PasteToActiveWindow();
    }
}
