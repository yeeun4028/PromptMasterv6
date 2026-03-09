namespace PromptMasterv6.Core.Interfaces
{
    public interface IClipboardService
    {
        void SetClipboard(string text);
        string? GetClipboard();
        bool ContainsText();
        bool ContainsImage();
        System.Windows.Media.Imaging.BitmapSource? GetImage();
    }
}
