using Microsoft.Xaml.Behaviors;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace PromptMasterv6.Infrastructure.Behaviors;

public class AutoNumberListBehavior : Behavior<System.Windows.Controls.TextBox>
{
    private DateTime _lastEnterTime = DateTime.MinValue;

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(AutoNumberListBehavior));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty EnableDoubleEnterSendProperty =
        DependencyProperty.Register(nameof(EnableDoubleEnterSend), typeof(bool), typeof(AutoNumberListBehavior), new PropertyMetadata(true));

    public bool EnableDoubleEnterSend
    {
        get => (bool)GetValue(EnableDoubleEnterSendProperty);
        set => SetValue(EnableDoubleEnterSendProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        base.OnDetaching();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        bool isCtrlEnter = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var now = DateTime.Now;
        var span = (now - _lastEnterTime).TotalMilliseconds;

        if (isCtrlEnter)
        {
            e.Handled = true;
            ExecuteCommand();
            return;
        }

        if (span < 500 && EnableDoubleEnterSend)
        {
            e.Handled = true;
            ExecuteCommand();
            _lastEnterTime = DateTime.MinValue;
            return;
        }

        _lastEnterTime = now;

        TryInsertNextNumber();
    }

    private void TryInsertNextNumber()
    {
        var textBox = AssociatedObject;
        int caretIndex = textBox.CaretIndex;
        int lineIndex = textBox.GetLineIndexFromCharacterIndex(caretIndex);
        if (lineIndex < 0) return;

        string lineText = textBox.GetLineText(lineIndex);
        var match = Regex.Match(lineText, @"^(\s*)(\d+)\.(\s+)");
        if (match.Success)
        {
            string indentation = match.Groups[1].Value;
            int currentNumber = int.Parse(match.Groups[2].Value);
            string spacing = match.Groups[3].Value;
            string insertText = $"\n{indentation}{currentNumber + 1}.{spacing}";
            textBox.SelectedText = insertText;
            textBox.CaretIndex += insertText.Length;
        }
    }

    private void ExecuteCommand()
    {
        if (Command?.CanExecute(null) == true)
        {
            Command.Execute(null);
        }
    }
}
