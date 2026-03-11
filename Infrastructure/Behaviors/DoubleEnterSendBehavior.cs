using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Input;

namespace PromptMasterv6.Infrastructure.Behaviors;

public class DoubleEnterSendBehavior : Behavior<System.Windows.Controls.TextBox>
{
    private DateTime _lastEnterTime = DateTime.MinValue;

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(DoubleEnterSendBehavior));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty EnableDoubleEnterSendProperty =
        DependencyProperty.Register(nameof(EnableDoubleEnterSend), typeof(bool), typeof(DoubleEnterSendBehavior), new PropertyMetadata(true));

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
    }

    private void ExecuteCommand()
    {
        if (Command?.CanExecute(null) == true)
        {
            Command.Execute(null);
        }
    }
}
