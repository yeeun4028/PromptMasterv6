namespace PromptMasterv5.Core.Interfaces
{
    public interface IDialogService
    {
        void ShowAlert(string message, string title);
        bool ShowConfirmation(string message, string title);
        string? ShowOpenFileDialog(string filter);
        string[]? ShowOpenFilesDialog(string filter);
        string? ShowSaveFileDialog(string filter, string defaultName);
        string? ShowFolderBrowserDialog(string description = "");
    }
}
