using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PromptMasterv6.Features.Main.ContentEditor
{
    public partial class ContentEditorView : System.Windows.Controls.UserControl
    {
        public ContentEditorView()
        {
            InitializeComponent();
        }

        private void ContentEditorTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is ContentEditorViewModel vm && vm.IsEditMode)
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                if (IsContentEditor(focused)) return;

                vm.IsEditMode = false;
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
