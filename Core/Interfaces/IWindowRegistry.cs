using System.Windows;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IWindowRegistry
    {
        void RegisterWindow<TViewModel, TWindow>() where TWindow : Window;
        Window? CreateWindow(object viewModel);
        Window? CreateWindow<TViewModel>();
        Window? CreateWindow(string viewModelName);
    }
}
