using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PromptMasterv6.Features.Main.ContentEditor
{
    public partial class ContentEditorView : System.Windows.Controls.UserControl
    {
        private ContentEditorViewModel? ViewModel => DataContext as ContentEditorViewModel;

        public ContentEditorView() : this(App.Services.GetRequiredService<ContentEditorViewModel>())
        {
        }

        public ContentEditorView(ContentEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void ContentEditorTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.IsEditMode)
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                if (IsContentEditor(focused)) return;

                ViewModel.IsEditMode = false;
            }
        }

        private bool IsContentEditor(DependencyObject? obj)
        {
            if (obj == null) return false;
            if (obj == ContentEditorTextBox) return true;
            
            if (ContentEditorTextBox != null && ContentEditorTextBox.IsAncestorOf(obj)) return true;
            
            return false;
        }
    }
}
