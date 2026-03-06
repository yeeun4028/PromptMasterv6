using System.Threading.Tasks;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IAudioService
    {
        Task PlayShutterSoundAsync();
        Task PlaySuccessSoundAsync();
        Task PlayErrorSoundAsync();
    }
}
