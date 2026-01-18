using GongSolutions.Wpf.DragDrop;
using PromptMasterv5.Core.Models;
using System.Windows;
using DragDropEffects = System.Windows.DragDropEffects;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;

namespace PromptMasterv5.ViewModels
{
    public class PinnedPromptDropHandler : IDropTarget
    {
        private readonly MainViewModel _viewModel;

        public PinnedPromptDropHandler(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is PromptItem && dropInfo.TargetItem is PromptItem)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not PromptItem source || dropInfo.TargetItem is not PromptItem) return;

            int oldIndex = _viewModel.MiniPinnedPrompts.IndexOf(source);
            int newIndex = dropInfo.InsertIndex;
            if (oldIndex < 0) return;
            if (oldIndex < newIndex) newIndex--;
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= _viewModel.MiniPinnedPrompts.Count) newIndex = _viewModel.MiniPinnedPrompts.Count - 1;
            if (oldIndex == newIndex) return;

            _viewModel.ReorderMiniPinnedPrompts(oldIndex, newIndex);
        }
    }
}
