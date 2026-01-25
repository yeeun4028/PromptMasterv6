using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IAudioService
    {
        Task PlayShutterSoundAsync();
        Task PlaySuccessSoundAsync();
        Task PlayErrorSoundAsync();
    }
}
