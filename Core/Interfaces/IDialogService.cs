namespace PromptMasterv6.Core.Interfaces
{
    public interface IDialogService
    {
        void ShowAlert(string message, string title);
        bool ShowConfirmation(string message, string title);
        void ShowToast(string message, string type = "Info");
        string? ShowOpenFileDialog(string filter);
        string[]? ShowOpenFilesDialog(string filter);
        string? ShowSaveFileDialog(string filter, string defaultName);
        string? ShowFolderBrowserDialog(string description = "");
        bool ShowOcrNotConfiguredDialog();
    }
}
