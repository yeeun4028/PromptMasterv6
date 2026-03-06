using System;
using System.Windows;
using PromptMasterv6.ViewModels;

namespace PromptMasterv6.Views
{
    public partial class VoiceControlWindow : Window
    {
        public VoiceControlWindow(VoiceControlViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose = () => 
            {
                this.Close();
            };
            
            this.Loaded += VoiceControlWindow_Loaded;
        }

        private void VoiceControlWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position at bottom center
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = screenHeight - this.Height - 100; // 100px from bottom logic
        }
    }
}
