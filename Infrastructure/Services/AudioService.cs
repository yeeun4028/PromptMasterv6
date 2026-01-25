using PromptMasterv5.Core.Interfaces;
using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;

namespace PromptMasterv5.Infrastructure.Services
{
    public class AudioService : IAudioService
    {
        private const string ShutterSoundFile = "Resources/shutter.wav";

        public async Task PlayShutterSoundAsync()
        {
             await Task.Run(() =>
             {
                 try
                 {
                     string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ShutterSoundFile);
                     if (File.Exists(path))
                     {
                         using (var player = new SoundPlayer(path))
                         {
                             player.Play();
                         }
                     }
                     else
                     {
                         // Fallback mechanism if custom sound is missing
                         // SystemSounds.Exclamation.Play(); // Or maybe nothing to avoid annoyance if file missing
                         // But request says "Classic camera shutter", so maybe we just log warning?
                         // Or try to play a "Ding" if file missing?
                         // Let's stick to checking file. If missing, maybe just log.
                         // Actually, for "Result State", some feedback is better than none.
                         // But usually Shutter sound is very specific.
                         System.Diagnostics.Debug.WriteLine($"Audio file not found: {path}");
                     }
                 }
                 catch (Exception ex)
                 {
                     System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
                 }
             });
        }

        public Task PlaySuccessSoundAsync()
        {
            return Task.Run(() => SystemSounds.Asterisk.Play());
        }

        public Task PlayErrorSoundAsync()
        {
             return Task.Run(() => SystemSounds.Hand.Play());
        }
    }
}
