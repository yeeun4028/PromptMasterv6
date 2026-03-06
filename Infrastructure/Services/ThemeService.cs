using PromptMasterv6.Core.Models;
using System.Windows;
using Application = System.Windows.Application;

namespace PromptMasterv6.Infrastructure.Services
{
    /// <summary>
    /// 主题服务
    /// 负责应用程序主题的切换和管理
    /// </summary>
    public class ThemeService
    {
        /// <summary>
        /// 应用指定主题
        /// </summary>
        public void ApplyTheme(ThemeType theme)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            if (theme == ThemeType.Dark)
            {
                SetBrush(resources, "ShellBackground", "#2E3033");
                SetBrush(resources, "AppBackground", "#363B40");
                SetBrush(resources, "SidebarBackground", "#2E3033");
                SetBrush(resources, "CardBackground", "#363B40");
                SetBrush(resources, "TextPrimary", "#ACBFBE");
                SetBrush(resources, "TextSecondary", "#ACBFBE");
                SetBrush(resources, "DividerColor", "#4A4F55");
                SetBrush(resources, "HintBrush", "#ACBFBE");
                SetBrush(resources, "ListItemHoverBackgroundBrush", "#3A3F45");
                SetBrush(resources, "ListItemSelectedBackgroundBrush", "#444A52");
                SetBrush(resources, "InputFocusBackgroundBrush", "#2E3033");

                SetBrush(resources, "Block1Background", "#2E3033");
                SetBrush(resources, "Block2Background", "#2E3033");
                SetBrush(resources, "Block3Background", "#363B40");
                SetBrush(resources, "Block4Background", "#363B40");
                SetBrush(resources, "PrimaryTextBrush", "#ACBFBE");
                SetBrush(resources, "SecondaryTextBrush", "#ACBFBE");
                SetBrush(resources, "PlaceholderTextBrush", "#ACBFBE");
                SetBrush(resources, "DividerBrush", "#4A4F55");
                SetBrush(resources, "ActionIconBrush", "#ACBFBE");
                SetBrush(resources, "ActionIconHoverBrush", "#ACBFBE");
                SetBrush(resources, "HeaderIconBrush", "#ACBFBE");
                SetBrush(resources, "HeaderIconHoverBrush", "#ACBFBE");

                SetBrush(resources, "Block3EditorTextBrush", "#B8BFC6");
                SetBrush(resources, "Block3EditorCaretBrush", "#B8BFC6");
                SetBrush(resources, "Block3EditorSelectionBrush", "#4A89DC");
                SetBrush(resources, "InputTextBrush", "#DEDEDE");
            }
            else
            {
                SetBrush(resources, "ShellBackground", "#FAFAFA");
                SetBrush(resources, "AppBackground", "#F1F1EF");
                SetBrush(resources, "SidebarBackground", "#F7F7F7");
                SetBrush(resources, "CardBackground", "#FFFFFF");
                SetBrush(resources, "TextPrimary", "#333333");
                SetBrush(resources, "TextSecondary", "#666666");
                SetBrush(resources, "DividerColor", "#E5E5E5");
                SetBrush(resources, "HintBrush", "#999999");
                SetBrush(resources, "ListItemHoverBackgroundBrush", "#EAEAEA");
                SetBrush(resources, "ListItemSelectedBackgroundBrush", "#E0E0E0");
                SetBrush(resources, "InputFocusBackgroundBrush", "#FFFFFF");

                SetBrush(resources, "Block1Background", "#E8E7E7");
                SetBrush(resources, "Block2Background", "#E8E7E7");
                SetBrush(resources, "Block3Background", "#EDEDED");
                SetBrush(resources, "Block4Background", "#EDEDED");
                SetBrush(resources, "PrimaryTextBrush", "#333333");
                SetBrush(resources, "SecondaryTextBrush", "#666666");
                SetBrush(resources, "PlaceholderTextBrush", "#999999");
                SetBrush(resources, "DividerBrush", "#E5E5E5");
                SetBrush(resources, "ActionIconBrush", "#666666");
                SetBrush(resources, "ActionIconHoverBrush", "#333333");
                SetBrush(resources, "HeaderIconBrush", "#666666");
                SetBrush(resources, "HeaderIconHoverBrush", "#333333");

                SetBrush(resources, "Block3EditorTextBrush", "#333333");
                SetBrush(resources, "Block3EditorCaretBrush", "#333333");
                SetBrush(resources, "Block3EditorSelectionBrush", "#4A89DC");
                SetBrush(resources, "InputTextBrush", "#666666");
            }
        }

        private static void SetBrush(ResourceDictionary resources, string key, string color)
        {
            resources[key] = new System.Windows.Media.SolidColorBrush(ParseColor(color));
        }

        private static System.Windows.Media.Color ParseColor(string value) =>
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
    }
}
