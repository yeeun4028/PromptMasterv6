using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Infrastructure.Services
{
    public class WindowRegistry : IWindowRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, Type> _windowMappings = new();
        private readonly Dictionary<string, Type> _windowMappingsByName = new();

        private Func<System.Drawing.Bitmap, Func<byte[], Rect, Task>?, Window>? _screenCaptureOverlayFactory;
        private Func<string, Window>? _translationPopupFactory;
        private Action<BitmapSource, PinToScreenOptions?, System.Windows.Point?>? _pinToScreenAction;
        private Action? _closeAllPinToScreenAction;
        private Func<int>? _pinToScreenCountProvider;

        public WindowRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void RegisterWindow<TViewModel, TWindow>() where TWindow : Window
        {
            var viewModelType = typeof(TViewModel);
            var windowType = typeof(TWindow);
            _windowMappings[viewModelType] = windowType;
            _windowMappingsByName[viewModelType.Name] = windowType;
        }

        public Window? CreateWindow(object viewModel)
        {
            var viewModelType = viewModel.GetType();
            if (!_windowMappings.TryGetValue(viewModelType, out var windowType))
            {
                return null;
            }

            var window = (Window?)_serviceProvider.GetService(windowType);
            if (window != null)
            {
                window.DataContext = viewModel;
            }
            return window;
        }

        public Window? CreateWindow<TViewModel>()
        {
            if (!_windowMappings.TryGetValue(typeof(TViewModel), out var windowType))
            {
                return null;
            }

            var window = (Window?)_serviceProvider.GetService(windowType);
            var vm = _serviceProvider.GetService(typeof(TViewModel));
            if (window != null && vm != null)
            {
                window.DataContext = vm;
            }
            return window;
        }

        public Window? CreateWindow(string viewModelName)
        {
            if (!_windowMappingsByName.TryGetValue(viewModelName, out var windowType))
            {
                return null;
            }

            var window = (Window?)_serviceProvider.GetService(windowType);
            if (window == null) return null;

            var viewModelTypeName = $"PromptMasterv6.Features.{viewModelName.Replace("ViewModel", "")}.{viewModelName}, PromptMasterv6";
            var viewModelType = Type.GetType(viewModelTypeName);
            if (viewModelType != null)
            {
                var vm = _serviceProvider.GetService(viewModelType);
                if (vm != null)
                {
                    window.DataContext = vm;
                }
            }
            return window;
        }

        public void RegisterScreenCaptureOverlay(Func<System.Drawing.Bitmap, Func<byte[], Rect, Task>?, Window> factory)
        {
            _screenCaptureOverlayFactory = factory;
        }

        public Window CreateScreenCaptureOverlay(System.Drawing.Bitmap screenBitmap, Func<byte[], Rect, Task>? onCaptureProcessing)
        {
            if (_screenCaptureOverlayFactory == null)
                throw new InvalidOperationException("ScreenCaptureOverlay factory not registered");
            return _screenCaptureOverlayFactory(screenBitmap, onCaptureProcessing);
        }

        public void RegisterTranslationPopup(Func<string, Window> factory)
        {
            _translationPopupFactory = factory;
        }

        public Window CreateTranslationPopup(string text)
        {
            if (_translationPopupFactory == null)
                throw new InvalidOperationException("TranslationPopup factory not registered");
            return _translationPopupFactory(text);
        }

        public void RegisterPinToScreen(Action<BitmapSource, PinToScreenOptions?, System.Windows.Point?> pinAction)
        {
            _pinToScreenAction = pinAction;
        }

        public void PinToScreen(BitmapSource image, PinToScreenOptions? options, System.Windows.Point? location)
        {
            _pinToScreenAction?.Invoke(image, options, location);
        }

        public void RegisterPinToScreenCloseAll(Action closeAllAction)
        {
            _closeAllPinToScreenAction = closeAllAction;
        }

        public void CloseAllPinToScreenWindows()
        {
            _closeAllPinToScreenAction?.Invoke();
        }

        public void RegisterPinToScreenCountProvider(Func<int> countProvider)
        {
            _pinToScreenCountProvider = countProvider;
        }

        public int GetPinToScreenWindowCount()
        {
            return _pinToScreenCountProvider?.Invoke() ?? 0;
        }
    }
}
