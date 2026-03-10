# ChangeActionIconCommand 功能缺失问题

## 问题描述

在 `MainWindow.xaml` 中，有 4 处按钮绑定了 `ChangeActionIconCommand` 命令，但该命令在代码中不存在，导致功能缺失。

## 影响范围

### XAML 绑定位置

| 行号 | 控件 | CommandParameter |
|------|------|------------------|
| 546 | 新建文档按钮右键菜单 | `"CreateFile"` |
| 563 | 新建文件夹按钮右键菜单 | `"CreateFolder"` |
| 580 | 导入按钮右键菜单 | `"Import"` |
| 597 | 设置按钮右键菜单 | `"Settings"` |

### 相关代码

```xml
<Button.ContextMenu>
    <ContextMenu>
        <MenuItem Header="设置图标..."
                  Command="{Binding ChangeActionIconCommand}"
                  CommandParameter="CreateFile"/>
    </ContextMenu>
</Button.ContextMenu>
```

## 预期功能

用户右键点击侧边栏底部的操作按钮（新建文档、新建文件夹、导入、设置），选择"设置图标..."后，应该能够：
1. 弹出图标选择对话框
2. 选择新的图标
3. 将图标保存到 `LocalConfig.ActionIcons` 字典中
4. 按钮图标更新为新选择的图标

## 数据结构

### LocalConfig.ActionIcons

```csharp
// 位于 LocalSettings 类中
public Dictionary<string, string> ActionIcons { get; set; } = new()
{
    { "CreateFile", "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 14h-2v-4H6v-2h4V7h2v4h4v2h-4v4z" },
    { "CreateFolder", "M10 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z" },
    { "Import", "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z" },
    { "Settings", "M19.14 12.94c.04-.31.06-.63.06-.94 0-.31-.02-.63-.06-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.04.31-.06.63-.06.94s.02.63.06.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z" }
};
```

### 图标显示绑定

```xml
<Path Data="{Binding DataContext.LocalConfig.ActionIcons[CreateFile], 
       RelativeSource={RelativeSource AncestorType=Window}, 
       Converter={StaticResource StringToGeometryConverter}, 
       FallbackValue={StaticResource IconNewDoc}}" 
      Style="{StaticResource ActionIconPathStyle}"/>
```

## 实现建议

### 方案一：在 MainViewModel 中实现

```csharp
// MainViewModel.cs
[RelayCommand]
private void ChangeActionIcon(string? actionKey)
{
    if (string.IsNullOrWhiteSpace(actionKey)) return;
    
    // 获取当前图标
    string currentIcon = LocalConfig.ActionIcons.GetValueOrDefault(actionKey, "");
    
    // 弹出图标选择对话框
    var dialog = new IconInputDialog(currentIcon);
    if (dialog.ShowDialog() == true)
    {
        // 更新配置
        LocalConfig.ActionIcons[actionKey] = dialog.ResultGeometry;
        _settingsService.SaveLocalSettings();
        
        // 通知 UI 更新
        OnPropertyChanged(nameof(LocalConfig));
    }
}
```

### 方案二：创建专门的图标管理服务

```csharp
// Infrastructure/Services/ActionIconService.cs
public class ActionIconService
{
    private readonly SettingsService _settingsService;
    
    public ActionIconService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    
    public void SetActionIcon(string actionKey, string iconGeometry)
    {
        _settingsService.LocalConfig.ActionIcons[actionKey] = iconGeometry;
        _settingsService.SaveLocalSettings();
    }
    
    public string GetActionIcon(string actionKey)
    {
        return _settingsService.LocalConfig.ActionIcons.GetValueOrDefault(actionKey, "");
    }
}
```

## 优先级

**低优先级** - 这是一个增强功能，不影响核心功能使用。用户可以暂时忽略右键菜单中的"设置图标..."选项。

## 相关文件

- `Features/Main/MainWindow.xaml` - XAML 绑定
- `Features/Main/MainViewModel.cs` - 建议实现位置
- `Features/Shared/Models/LocalSettings.cs` - 数据模型
- `Features/Shared/Dialogs/IconInputDialog.cs` - 图标选择对话框（已存在）
- `Infrastructure/Services/SettingsService.cs` - 配置保存服务

## 验证方法

实现后，按以下步骤验证：

1. 启动应用程序
2. 右键点击"新建文档"按钮
3. 选择"设置图标..."
4. 在弹出的对话框中选择新图标
5. 确认按钮图标已更新
6. 重启应用，确认图标设置已保存
