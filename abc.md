# ParseVariablesRealTime 完整实现分析

## 1. VariableRegex 声明

```csharp
private static readonly Regex VariableRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);
```

**说明：**
- 静态只读字段，使用 `RegexOptions.Compiled` 优化性能
- 匹配模式：`{{变量名}}` 格式（双花括号包裹）

---

## 2. Variables 集合声明

```csharp
[ObservableProperty] private ObservableCollection<VariableItem> variables = new();
```

**说明：**
- 类型：`ObservableCollection<VariableItem>`
- 使用 `[ObservableProperty]` 特性自动生成属性变更通知
- 集合本身支持 UI 自动更新（增删元素时）

---

## 3. OnSelectedFileChanged 到 ParseVariablesRealTime 的调用链

```csharp
partial void OnSelectedFileChanged(PromptItem? value)
{
    OnPropertyChanged(nameof(HasVariables));
    OnPropertyChanged(nameof(Variables));
    IsEditMode = false;
    PreviewContent = ConvertHtmlToMarkdown(value?.Content);
    ParseVariablesRealTime(value?.Content ?? "");  // ← 同步调用
}
```

**调用链分析：**

| 调用位置 | 是否异步 | 是否跨线程 |
|---------|---------|-----------|
| `OnSelectedFileChanged` | ❌ 同步 | ❌ UI 线程 |
| `ParseVariablesRealTime` | ❌ 同步 | ❌ UI 线程 |

**结论：**
- **不存在 async/await 或 Task.Run 等跨线程操作**
- 整个调用链都是同步执行，在 UI 线程上运行
- `ObservableCollection` 的操作是线程安全的（仅在 UI 线程操作）

---

## 4. ParseVariablesRealTime 完整实现

```csharp
private void ParseVariablesRealTime(string content)
{
    if (string.IsNullOrEmpty(content))
    {
        Variables.Clear();
        HasVariables = false;
        return;
    }

    var matches = VariableRegex.Matches(content);
    var newVarNames = matches.Cast<Match>()
        .Select(m => m.Groups[1].Value.Trim())
        .Where(s => !string.IsNullOrEmpty(s))
        .Distinct()
        .ToList();

    for (int i = Variables.Count - 1; i >= 0; i--)
    {
        if (!newVarNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
    }

    foreach (var name in newVarNames)
    {
        if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
    }

    HasVariables = Variables.Count > 0;
}
```

---

## 5. 其他调用位置

```csharp
// 第 993 行 - 另一处调用
ParseVariablesRealTime(SelectedFile?.Content ?? "");
```

---

## 6. 变量使用示例

| 输入内容 | 匹配结果 |
|---------|---------|
| `Hello {{name}}` | `["name"]` |
| `{{user}} 和 {{project}}` | `["user", "project"]` |
| `{{  variable  }}` | `["variable"]`（自动 Trim） |
