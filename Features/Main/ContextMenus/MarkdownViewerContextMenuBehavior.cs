using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PromptMasterv6.Features.Main.ContextMenus;

public static class MarkdownViewerContextMenuBehavior
{
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(MarkdownViewerContextMenuBehavior),
            new PropertyMetadata(false, OnAttachChanged));

    public static bool GetAttach(DependencyObject obj)
    {
        return (bool)obj.GetValue(AttachProperty);
    }

    public static void SetAttach(DependencyObject obj, bool value)
    {
        obj.SetValue(AttachProperty, value);
    }

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            if ((bool)e.NewValue)
            {
                element.Loaded += OnElementLoaded;
            }
            else
            {
                element.Loaded -= OnElementLoaded;
            }
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement root) return;

        root.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            var fdsv = FindDescendant<FlowDocumentScrollViewer>(root);
            if (fdsv != null)
            {
                fdsv.ContextMenu = EditorContextMenuBuilder.Build();
            }
        });
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var nested = FindDescendant<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
