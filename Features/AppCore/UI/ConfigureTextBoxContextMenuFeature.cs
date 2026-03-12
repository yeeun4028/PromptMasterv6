using MediatR;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Threading;
using System.Threading.Tasks;

using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace PromptMasterv6.Features.AppCore.UI;

/// <summary>
/// 配置 TextBox 上下文菜单功能
/// 负责为应用程序中的所有 TextBox 控件提供统一的右键菜单样式
/// </summary>
public static class ConfigureTextBoxContextMenuFeature
{
    // 1. 定义输入
    /// <summary>
    /// 配置 TextBox 上下文菜单命令
    /// </summary>
    public record Command : IRequest<Result>;

    // 2. 定义输出
    /// <summary>
    /// 配置结果
    /// </summary>
    /// <param name="Success">是否成功</param>
    /// <param name="Message">结果消息</param>
    public record Result(bool Success, string Message);

    // 3. 执行逻辑
    /// <summary>
    /// 配置 TextBox 上下文菜单处理器
    /// </summary>
    public class Handler : IRequestHandler<Command, Result>
    {
        /// <summary>
        /// 构造函数 - 当前 Feature 不需要额外依赖
        /// </summary>
        public Handler()
        {
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                // 注册全局类处理器，为所有 TextBox 提供统一的上下文菜单
                EventManager.RegisterClassHandler(
                    typeof(System.Windows.Controls.TextBox),
                    ContextMenuService.ContextMenuOpeningEvent,
                    new ContextMenuEventHandler(OnTextBoxContextMenuOpening));

                return Task.FromResult(new Result(true, "TextBox 上下文菜单配置成功"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new Result(false, $"配置失败: {ex.Message}"));
            }
        }

        private static void OnTextBoxContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb) return;

            var menu = CreateContextMenu();
            tb.ContextMenu = menu;
        }

        private static ContextMenu CreateContextMenu()
        {
            var menu = new ContextMenu
            {
                Background = (WpfBrush)System.Windows.Application.Current.Resources["CardBackground"],
                BorderBrush = (WpfBrush)System.Windows.Application.Current.Resources["DividerBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 4, 0, 4),
            };

            menu.Template = CreateMenuTemplate();

            menu.Items.Add(CreateMenuItem("剪切", ApplicationCommands.Cut));
            menu.Items.Add(CreateMenuItem("复制", ApplicationCommands.Copy));
            menu.Items.Add(CreateMenuItem("粘贴", ApplicationCommands.Paste));

            var sep = new Separator { Margin = new Thickness(0, 2, 0, 2) };
            if (System.Windows.Application.Current.Resources["DividerBrush"] is WpfBrush divBrush)
                sep.Background = divBrush;
            menu.Items.Add(sep);

            menu.Items.Add(CreateMenuItem("全选", ApplicationCommands.SelectAll));

            return menu;
        }

        private static ControlTemplate CreateMenuTemplate()
        {
            var menuTemplate = new ControlTemplate(typeof(ContextMenu));
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

            var shadow = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 4,
                BlurRadius = 12,
                Opacity = 0.3
            };
            borderFactory.SetValue(Border.EffectProperty, shadow);

            var itemsPresenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter));
            borderFactory.AppendChild(itemsPresenterFactory);
            menuTemplate.VisualTree = borderFactory;

            return menuTemplate;
        }

        private static MenuItem CreateMenuItem(string header, ICommand command)
        {
            var item = new MenuItem
            {
                Header = header,
                Command = command,
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            var itemTemplate = new ControlTemplate(typeof(MenuItem));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "Bd";
            bd.SetValue(Border.BackgroundProperty, WpfBrushes.Transparent);
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

            var highlight = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            highlight.Setters.Add(new Setter(Border.BackgroundProperty,
                System.Windows.Application.Current.Resources.Contains("ListItemSelectedBackgroundBrush")
                    ? System.Windows.Application.Current.Resources["ListItemSelectedBackgroundBrush"]
                    : WpfBrushes.LightGray, "Bd"));
            itemTemplate.Triggers.Add(highlight);

            var disabled = new Trigger { Property = MenuItem.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
            itemTemplate.Triggers.Add(disabled);

            item.Template = itemTemplate;

            if (System.Windows.Application.Current.Resources["PrimaryTextBrush"] is WpfBrush fg)
                item.Foreground = fg;

            return item;
        }
    }
}
