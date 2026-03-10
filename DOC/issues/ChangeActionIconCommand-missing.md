# ChangeActionIconCommand 功能缺失问题

## 问题描述

在 `MainWindow.xaml` 中，有 4 处按钮绑定了 `ChangeActionIconCommand` 命令，但该命令在代码中不存在，导致功能缺失。

## 根本原因

**WPF 视觉树断裂问题**：`ContextMenu` 是独立于主窗口的弹出层（Popup），它不在 Button 的视觉树链条上，默认 `DataContext` 是 `null`。直接绑定 `Command="{Binding ChangeActionIconCommand}"` 会导致无效绑定。

## 影响范围

### XAML 绑定位置

| 行号 | 控件 | CommandParameter |
|------|------|------------------|
| 546 | 新建文档按钮右键菜单 | `"CreateFile"` |
| 563 | 新建文件夹按钮右键菜单 | `"CreateFolder"` |
| 580 | 导入按钮右键菜单 | `"Import"` |
| 597 | 设置按钮右键菜单 | `"Settings"` |

## 修复方案

### 第一步：在 MainViewModel 中实现命令

```csharp
// MainViewModel.cs
[RelayCommand]
private void ChangeActionIcon(string actionKey)
{
    if (string.IsNullOrWhiteSpace(actionKey)) return;

    var currentIcon = LocalConfig.ActionIcons != null && LocalConfig.ActionIcons.TryGetValue(actionKey, out var icon) ? icon : "";

    var dialog = new IconInputDialog(currentIcon);
    if (dialog.ShowDialog() == true)
    {
        if (LocalConfig.ActionIcons == null)
        {
            LocalConfig.ActionIcons = new System.Collections.Generic.Dictionary<string, string>();
        }

        LocalConfig.ActionIcons[actionKey] = dialog.ResultGeometry;
        
        _settingsService.SaveLocalConfig();

        // WPF 数据绑定黑魔法：
        // 原生 Dictionary 不支持 INotifyCollectionChanged。
        // 必须强制让绑定引擎重新评估 LocalConfig 下的所有绑定
        OnPropertyChanged(nameof(LocalConfig));
    }
}
```

### 第二步：修复 XAML 中的 ContextMenu 视觉树断裂

利用 `PlacementTarget` 作为跳板，把绑定连回主视觉树的 `DataContext`：

```xml
<!-- 修复前（无效绑定）-->
<MenuItem Header="设置图标..."
          Command="{Binding ChangeActionIconCommand}"
          CommandParameter="CreateFile"/>

<!-- 修复后（正确绑定）-->
<MenuItem Header="设置图标..."
          Command="{Binding PlacementTarget.DataContext.ChangeActionIconCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"
          CommandParameter="CreateFile"/>
```

## 技术要点

### WPF ContextMenu 视觉树机制

```
主窗口视觉树:
Window → Grid → Button → ContextMenu (独立弹出层)
                                      ↓
                              DataContext = null (默认)
                              
修复后的绑定路径:
ContextMenu.PlacementTarget (Button) → DataContext (MainViewModel) → ChangeActionIconCommand
```

### Dictionary 绑定刷新问题

`Dictionary<TKey, TValue>` 不实现 `INotifyCollectionChanged`，修改字典内容不会自动刷新 UI。解决方案是触发 `OnPropertyChanged(nameof(LocalConfig))` 强制刷新整个对象的绑定。

## 相关文件

- `Features/Main/MainWindow.xaml` - XAML 绑定（已修复）
- `Features/Main/MainViewModel.cs` - 命令实现（已添加）
- `Features/Shared/Models/LocalSettings.cs` - 数据模型
- `Features/Shared/Dialogs/IconInputDialog.cs` - 图标选择对话框
- `Infrastructure/Services/SettingsService.cs` - 配置保存服务

## 状态

✅ **已修复** - 2024年

## 验证方法

1. 启动应用程序
2. 右键点击"新建文档"按钮
3. 选择"设置图标..."
4. 在弹出的对话框中选择新图标
5. 确认按钮图标已更新
6. 重启应用，确认图标设置已保存
