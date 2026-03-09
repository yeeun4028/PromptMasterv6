using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class WindowRegistry : IWindowRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, Type> _windowMappings = new();
        private readonly Dictionary<string, Type> _windowMappingsByName = new();

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
    }
}
