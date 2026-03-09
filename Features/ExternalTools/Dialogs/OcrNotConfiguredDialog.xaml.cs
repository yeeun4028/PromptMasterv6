using System.Windows;

namespace PromptMasterv6.Features.ExternalTools.Dialogs
{
    public partial class OcrNotConfiguredDialog : Window
    {
        public OcrNotConfiguredDialog()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void GoToSettings_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
