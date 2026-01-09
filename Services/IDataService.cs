// File: Services/IDataService.cs
using PromptMasterv5.Models;
using System.Collections.Generic;

namespace PromptMasterv5.Services
{
    /// <summary>
    /// 数据服务接口
    /// 定义了加载和保存的标准，ViewModel 只认这个接口，不关心具体是存硬盘还是存网盘
    /// </summary>
    public interface IDataService
    {
        AppData Load();
        void Save(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files);
    }

    /// <summary>
    /// 数据传输对象 (DTO)
    /// 用来一次性打包所有数据
    /// </summary>
    public class AppData
    {
        public List<FolderItem> Folders { get; set; } = new();
        public List<PromptItem> Files { get; set; } = new();
    }
}