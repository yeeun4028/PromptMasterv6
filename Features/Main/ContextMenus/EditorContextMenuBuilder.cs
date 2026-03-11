using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Effects;

namespace PromptMasterv6.Features.Main.ContextMenus;

public static class EditorContextMenuBuilder
{
    public static System.Windows.Controls.ContextMenu Build()
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            Background = System.Windows.Application.Current.Resources["CardBackground"] as System.Windows.Media.Brush,
            BorderBrush = System.Windows.Application.Current.Resources["DividerBrush"] as System.Windows.Media.Brush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0, 4, 0, 4),
        };

        var menuTemplate = CreateMenuTemplate();
        menu.Template = menuTemplate;

        var fg = System.Windows.Application.Current.Resources["PrimaryTextBrush"] as System.Windows.Media.Brush;
        var divBrush = System.Windows.Application.Current.Resources["DividerBrush"] as System.Windows.Media.Brush;

        menu.Items.Add(CreateMenuItem("复制", ApplicationCommands.Copy, fg!));
        menu.Items.Add(new Separator { Margin = new Thickness(0, 2, 0, 2), Background = divBrush });
        menu.Items.Add(CreateMenuItem("全选", ApplicationCommands.SelectAll, fg!));

        return menu;
    }

    private static ControlTemplate CreateMenuTemplate()
    {
        var menuTemplate = new ControlTemplate(typeof(System.Windows.Controls.ContextMenu));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        
        borderFactory.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding 
            { 
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), 
                Path = new PropertyPath(System.Windows.Controls.Control.BackgroundProperty) 
            });
        borderFactory.SetBinding(Border.BorderBrushProperty,
            new System.Windows.Data.Binding 
            { 
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), 
                Path = new PropertyPath(System.Windows.Controls.Control.BorderBrushProperty) 
            });
        borderFactory.SetBinding(Border.BorderThicknessProperty,
            new System.Windows.Data.Binding 
            { 
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), 
                Path = new PropertyPath(System.Windows.Controls.Control.BorderThicknessProperty) 
            });
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
        borderFactory.SetValue(Border.EffectProperty, new DropShadowEffect
        {
            Color = System.Windows.Media.Colors.Black,
            Direction = 270,
            ShadowDepth = 4,
            BlurRadius = 12,
            Opacity = 0.3
        });

        var itemsPresenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter));
        borderFactory.AppendChild(itemsPresenterFactory);
        menuTemplate.VisualTree = borderFactory;

        return menuTemplate;
    }

    private static MenuItem CreateMenuItem(string header, ICommand command, System.Windows.Media.Brush foreground)
    {
        var item = new MenuItem
        {
            Header = header,
            Command = command,
            Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = foreground,
        };

        var itemTemplate = CreateMenuItemTemplate();
        item.Template = itemTemplate;

        return item;
    }

    private static ControlTemplate CreateMenuItemTemplate()
    {
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.Name = "Bd";
        bd.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        bd.SetValue(Border.PaddingProperty, new Thickness(12, 3, 12, 3));
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetBinding(ContentPresenter.ContentProperty,
            new System.Windows.Data.Binding(nameof(MenuItem.Header))
            { 
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) 
            });
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
        bd.AppendChild(cp);
        itemTemplate.VisualTree = bd;

        var hlTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hlTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            System.Windows.Application.Current.Resources.Contains("ListItemSelectedBackgroundBrush")
                ? System.Windows.Application.Current.Resources["ListItemSelectedBackgroundBrush"]
                : System.Windows.Media.Brushes.LightGray, "Bd"));
        itemTemplate.Triggers.Add(hlTrigger);

        var disabledTrigger = new Trigger { Property = MenuItem.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        itemTemplate.Triggers.Add(disabledTrigger);

        return itemTemplate;
    }
}
