切除 `MainViewModel` 中关于“导入 Markdown 文件”的毒瘤，把它变成一个纯正、高内聚的垂直切片。

在垂直切片架构中，**ViewModel 绝对不能亲自去读写文件**，它只能负责两件事：收集用户的 UI 意图，以及在收到结果后更新 UI 状态。

以下是具体的、分步执行的解决方案：

### 第一步：重构 `ImportMarkdownFilesCommand.cs` (业务逻辑内聚)

目前的 `ImportMarkdownFilesCommand` 里混入了 `ObservableCollection` 这种 UI 层的控件对象，这是绝对的反模式。我们需要把它改造成只接收纯数据（DTO），并在 Handler 内部完成所有真正的业务逻辑（如读取文件内容、生成唯一 ID、构建实体）。

请打开 **`Features/Main/Import/ImportMarkdownFilesCommand.cs`**，将里面的内容**全部替换**为以下代码：

```csharp
using MediatR;
using PromptMasterv6.Features.Shared.Models; // 确保引用了 PromptItem 所在的命名空间
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Import
{
    // 1. Command 只接收纯数据：用户选中的文件路径数组，以及目标文件夹的 ID
    public record ImportMarkdownFilesCommand(string[] Files, string TargetFolderId) : IRequest<List<PromptItem>>;

    // 2. Handler 负责真正的业务逻辑（读取文件、构建实体对象）
    public class ImportMarkdownFilesHandler : IRequestHandler<ImportMarkdownFilesCommand, List<PromptItem>>
    {
        public async Task<List<PromptItem>> Handle(ImportMarkdownFilesCommand request, CancellationToken cancellationToken)
        {
            var importedItems = new List<PromptItem>();

            if (request.Files == null || request.Files.Length == 0)
                return importedItems;

            foreach (var filePath in request.Files)
            {
                // 允许中途取消
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // 真实的 IO 操作全部内聚在这里
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    var title = Path.GetFileNameWithoutExtension(filePath);
                    
                    var item = new PromptItem
                    {
                        Id = Guid.NewGuid().ToString("N"), // 生成唯一标识
                        Title = title,
                        Content = content,
                        FolderId = request.TargetFolderId,
                        LastModified = DateTime.Now
                    };
                    
                    importedItems.Add(item);
                }
                catch (Exception)
                {
                    // 在这里处理单一文件读取失败的情况
                    // 如果注入了 Logger，可以记录日志。不要抛出异常打断其他文件的导入
                }
            }

            // 返回构建好的纯数据集合
            return importedItems;
        }
    }
}

```

*(注：由于纯 UI 对话框的行为可以直接在 ViewModel 中调用 DialogService 完成，你可以直接删掉原本写在这里的 `ShowImportFileDialogCommand`，保持切片干净。)*

---

### 第二步：为 `MainViewModel` 注入 MediatR

要在 ViewModel 中发送我们刚写好的 Command，你需要拥有 `IMediator`。检查你的 `MainViewModel.cs`，目前它的构造函数里塞满了各种 Service，但唯独缺少了负责发号施令的 MediatR。

请打开 **`Features/Main/MainViewModel.cs`**，进行以下修改：

**1. 添加私有字段：**
在类顶部的私有字段声明区域，添加：

```csharp
private readonly IMediator _mediator;

```

**2. 修改构造函数：**
在 `MainViewModel` 的构造函数参数列表中加入 `IMediator mediator`，并赋值。

```csharp
    public MainViewModel(
        SettingsService settingsService,
        AiService aiService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService,
        GlobalKeyService keyService,
        DialogService dialogService,
        ClipboardService clipboardService,
        WindowManager windowManager,
        HotkeyService hotkeyService,
        VariableService variableService,
        ContentConverterService contentConverterService,
        WebTargetService webTargetService,
        LoggerService logger,
        IMediator mediator) // <--- 新增这一行
    {
        // ... 原有的代码保持不变 ...
        
        _logger = logger;
        _mediator = mediator; // <--- 新增这一行
        
        // ... 原有的代码保持不变 ...
    }

```

---

### 第三步：重写 ViewModel 中的导入方法 (剥离业务，保留 UI 状态)

最后，我们要把 `MainViewModel` 里面那个亲自动手读文件的 `ImportMarkdownFiles` 方法删掉，替换为一个**只负责 UI 调度和发送指令**的异步方法。

在 **`Features/Main/MainViewModel.cs`** 中，找到原来的 `private void ImportMarkdownFiles()` 方法，**将其替换为以下代码**：

```csharp
    [RelayCommand]
    private async Task ImportMarkdownFilesAsync() // 注意：由于涉及到 IO，方法必须改为 Async
    {
        // 1. 纯 UI 交互：弹出对话框获取文件路径
        string filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*";
        var files = _dialogService.ShowOpenFilesDialog(filter);

        if (files == null || files.Length == 0) return;

        // 2. 纯 UI 逻辑：确定目标文件夹状态，如果不存在则在 UI 集合中新建
        var targetFolder = SelectedFolder;
        if (targetFolder == null)
        {
            targetFolder = new FolderItem 
            { 
                Id = Guid.NewGuid().ToString("N"), // 必须有 ID 才能作为关联
                Name = "导入" 
            };
            Folders.Add(targetFolder);
            SelectedFolder = targetFolder;
        }

        // 3. 核心解耦：将"如何读取文件和创建对象"的业务逻辑委托给垂直切片的 Handler
        var importedItems = await _mediator.Send(new Features.Main.Import.ImportMarkdownFilesCommand(files, targetFolder.Id));

        // 4. 纯 UI 逻辑：拿到数据结果后，更新 ViewModels 的集合
        if (importedItems != null && importedItems.Any())
        {
            foreach (var item in importedItems)
            {
                Files.Add(item);
            }

            UpdateFilesViewFilter();
            FilesView?.Refresh();
            RequestSave(); // 触发界面的脏值标记与保存逻辑
        }
    }

```

### 

执行完这三步，你的“导入文件”功能就实现了真正的垂直切片隔离：
`MainViewModel` 退化成了一个纯粹的“交通警察”（只管弹窗、发送 Command、更新 ListBox 的数据源），而 `ImportMarkdownFilesHandler` 变成了一个强内聚的“业务工人”（只管读取硬盘、解析内容、吐出模型）。

成功之后，我们就可以继续用这套标准的 VSA 手术刀，去切除你 `MainViewModel` 里的 `VariableService` 和 `WebTargetService` 依赖