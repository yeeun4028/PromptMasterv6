using System;
using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IVoiceService
    {
        bool IsRecording { get; }
        
        // 音频电平变化事件（用于可视化）
        event EventHandler<float> OnAudioLevelChanged;
        
        // 录音开始事件
        event EventHandler OnRecordingStarted;
        
        // 录音停止事件
        event EventHandler OnRecordingStopped;
        
        // 中间结果事件（实时识别时触发，讯飞模式支持）
        event EventHandler<string> OnIntermediateResult;
        
        // 最终结果事件
        event EventHandler<string> OnFinalResult;
        
        // 错误事件
        event EventHandler<Exception> OnError;

        // 开始录音
        void StartRecording();
        
        // 停止录音并获取识别结果
        Task<string> StopRecordingAndTranscribeAsync(IReadOnlyList<string>? hotwords = null);
        
        // 取消录音
        void CancelRecording();
        
        // 配置更新
        void UpdateConfig(string baseUrl, string apiKey, string model);
        
        // 检查是否已配置
        Task<bool> IsConfiguredAsync();
        
        // 获取当前引擎名称
        string GetCurrentProviderName();
    }
}
