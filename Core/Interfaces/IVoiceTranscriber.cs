using System;
using System.Threading.Tasks;

namespace PromptMasterv6.Core.Interfaces
{
    /// <summary>
    /// 语音识别器接口
    /// 支持多种语音识别引擎的实现
    /// </summary>
    public interface IVoiceTranscriber : IDisposable
    {
        /// <summary>
        /// 引擎名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 是否正在录音
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// 音频电平变化事件（用于可视化）
        /// </summary>
        event EventHandler<float>? OnAudioLevelChanged;

        /// <summary>
        /// 录音开始事件
        /// </summary>
        event EventHandler? OnRecordingStarted;

        /// <summary>
        /// 录音停止事件
        /// </summary>
        event EventHandler? OnRecordingStopped;

        /// <summary>
        /// 中间结果事件（实时识别时触发）
        /// </summary>
        event EventHandler<string>? OnIntermediateResult;

        /// <summary>
        /// 最终结果事件
        /// </summary>
        event EventHandler<string>? OnFinalResult;

        /// <summary>
        /// 错误事件
        /// </summary>
        event EventHandler<Exception>? OnError;

        /// <summary>
        /// 开始录音
        /// </summary>
        void StartRecording();

        /// <summary>
        /// 停止录音并获取识别结果
        /// </summary>
        /// <param name="hotwords">热词列表（可选）</param>
        /// <returns>识别结果文本</returns>
        Task<string> StopRecordingAndTranscribeAsync(IReadOnlyList<string>? hotwords = null);

        /// <summary>
        /// 取消录音
        /// </summary>
        void CancelRecording();

        /// <summary>
        /// 检查配置是否有效
        /// </summary>
        /// <returns>配置是否有效</returns>
        Task<bool> IsConfiguredAsync();

        /// <summary>
        /// 更新配置
        /// </summary>
        /// <param name="baseUrl">API 基础地址</param>
        /// <param name="apiKey">API Key</param>
        /// <param name="model">模型名称</param>
        void UpdateConfig(string baseUrl, string apiKey, string model);
    }
}
