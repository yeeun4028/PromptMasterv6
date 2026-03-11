namespace PromptMasterv6.Features.Main.Components
{
    public partial class VariableInputView : System.Windows.Controls.UserControl
    {
        public VariableInputView()
        {
            InitializeComponent();
        }

        private void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var textBox = sender as System.Windows.Controls.TextBox;
                if (textBox == null) return;
                bool isCtrlEnter = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;
                if (isCtrlEnter)
                {
                    e.Handled = true;
                    if (DataContext is MainViewModel vm)
                    {
                        vm.ContentEditorVM.SendDefaultWebTargetCommand.Execute(null);
                    }
                }
            }
        }

        private void AdditionalInputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var textBox = sender as System.Windows.Controls.TextBox;
                if (textBox == null) return;
                bool isCtrlEnter = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;
                if (isCtrlEnter)
                {
                    e.Handled = true;
                    if (DataContext is MainViewModel vm)
                    {
                        vm.ContentEditorVM.SendDefaultWebTargetCommand.Execute(null);
                    }
                }
            }
        }
    }
}
