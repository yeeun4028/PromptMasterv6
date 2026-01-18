using GongSolutions.Wpf.DragDrop;
using System.Windows;
using PromptMasterv5.Core.Models;
using DragDropEffects = System.Windows.DragDropEffects;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;

namespace PromptMasterv5.ViewModels
{
    public class FolderDropHandler : IDropTarget
    {
        private readonly MainViewModel _viewModel;

        public FolderDropHandler(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            // 情况1：拖拽文件 -> 移动到文件夹
            if (dropInfo.Data is PromptItem && dropInfo.TargetItem is FolderItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight; // 高亮目标文件夹
                dropInfo.Effects = DragDropEffects.Move;
            }
            // 情况2：拖拽文件夹 -> 文件夹排序
            else if (dropInfo.Data is FolderItem && dropInfo.TargetItem is FolderItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert; // 显示插入线
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            // ★★★ 修复点：修改了变量名，防止冲突 ★★★

            // 处理情况1：移动文件
            // 将 targetFolder 改名为 fileTarget
            if (dropInfo.Data is PromptItem file && dropInfo.TargetItem is FolderItem fileTarget)
            {
                _viewModel.MoveFileToFolder(file, fileTarget);
            }
            // 处理情况2：文件夹排序
            // 将 targetFolder 改名为 folderTarget
            else if (dropInfo.Data is FolderItem sourceFolder && dropInfo.TargetItem is FolderItem folderTarget)
            {
                // 获取原来的位置和新位置
                int oldIndex = _viewModel.Folders.IndexOf(sourceFolder);
                int newIndex = dropInfo.InsertIndex;

                // 修正插入位置逻辑
                if (oldIndex < newIndex) newIndex--;
                if (oldIndex == newIndex) return;

                // 调用 ViewModel 里的排序方法
                _viewModel.ReorderFolders(oldIndex, newIndex);
            }
        }
    }
}