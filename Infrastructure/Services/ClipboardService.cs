using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;
using FormsClipboard = System.Windows.Forms.Clipboard;
using System.Windows.Forms; // For SendKeys
using System.Windows.Automation; // For UI Automation

namespace PromptMasterv5.Infrastructure.Services
{
    /// <summary>
    /// 剪贴板操作服务，用于全局划词助手功能
    /// </summary>
    public class ClipboardService
    {
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;
        private const int VK_V = 0x56;
        private const int KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// 模拟 Ctrl+C 获取当前选中的文本
        /// </summary>
        /// <returns>选中的文本，如果失败则返回 null</returns>
        public async Task<string?> GetSelectedTextAsync()
        {
            // 策略 A：优先使用 UI Automation（无障碍接口）
            var uiText = await TryGetTextViaUIAutomation();
            if (!string.IsNullOrWhiteSpace(uiText))
            {
                LoggerService.Instance.LogInfo($"UI Automation 成功获取文本: {uiText.Substring(0, Math.Min(50, uiText.Length))}...", "ClipboardService");
                return uiText;
            }

            // 策略 B：回退到剪贴板方式
            LoggerService.Instance.LogInfo("UI Automation 未获取到文本，尝试剪贴板方式", "ClipboardService");
            return await TryGetTextViaClipboard();
        }

        /// <summary>
        /// 使用 UI Automation 获取当前焦点元素的选中文本
        /// </summary>
        private async Task<string?> TryGetTextViaUIAutomation()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 获取当前焦点元素
                    var focusedElement = AutomationElement.FocusedElement;
                    if (focusedElement == null)
                    {
                        LoggerService.Instance.LogWarning("UI Automation: 未找到焦点元素", "ClipboardService");
                        return null;
                    }

                    // 尝试获取 TextPattern
                    object patternObj;
                    if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
                    {
                        var textPattern = (TextPattern)patternObj;
                        var selections = textPattern.GetSelection();
                        
                        if (selections != null && selections.Length > 0)
                        {
                            // 获取第一个选中区域的文本
                            string selectedText = selections[0].GetText(-1); // -1 表示获取全部
                            if (!string.IsNullOrWhiteSpace(selectedText))
                            {
                                return selectedText;
                            }
                        }
                    }

                    // 如果上面方法失败，尝试 ValuePattern (适用于某些控件如文本框)
                    if (focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out patternObj))
                    {
                        var valuePattern = (ValuePattern)patternObj;
                        return valuePattern.Current.Value;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogWarning($"UI Automation 获取文本失败: {ex.Message}", "ClipboardService");
                    return null;
                }
            });
        }

        private const int VK_MENU = 0x12; // Alt key

        /// <summary>
        /// 使用剪贴板方式获取选中文本（备选方案）
        /// </summary>
        private async Task<string?> TryGetTextViaClipboard()
        {
            try
            {
                // 1. 清空剪贴板 (必须在 UI 线程执行)
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try { FormsClipboard.Clear(); } catch { }
                });
                
                await Task.Delay(50).ConfigureAwait(false);

                // 2. 模拟按键 (使用 SendInput 以确保原子性)
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        // DEBUG: 记录当前活跃窗口，确认焦点是否正确
                        var hwnd = NativeMethods.GetForegroundWindow();
                        var sb = new System.Text.StringBuilder(256);
                        if (NativeMethods.GetWindowText(hwnd, sb, 256) > 0)
                        {
                            LoggerService.Instance.LogInfo($"准备发送复制指令到窗口: [{sb}] (Handle: {hwnd})", "ClipboardService");
                        }
                        else
                        {
                            LoggerService.Instance.LogInfo($"准备发送复制指令到窗口 Handle: {hwnd}", "ClipboardService");
                        }

                        // 构造 SendInput 输入序列
                        var inputs = new NativeMethods.INPUT[5];
                        int i = 0;

                        // [0] Alt Up (释放 Alt 键)
                        inputs[i++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = NativeMethods.VK_MENU,
                                    dwFlags = NativeMethods.KEYEVENTF_KEYUP
                                }
                            }
                        };

                        // [1] Ctrl Down
                        inputs[i++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = NativeMethods.VK_CONTROL,
                                    dwFlags = 0 // KeyDown
                                }
                            }
                        };

                        // [2] C Down
                        inputs[i++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = NativeMethods.VK_C,
                                    dwFlags = 0 // KeyDown
                                }
                            }
                        };

                        // [3] C Up
                        inputs[i++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = NativeMethods.VK_C,
                                    dwFlags = NativeMethods.KEYEVENTF_KEYUP
                                }
                            }
                        };

                        // [4] Ctrl Up
                        inputs[i++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = NativeMethods.VK_CONTROL,
                                    dwFlags = NativeMethods.KEYEVENTF_KEYUP
                                }
                            }
                        };
                        
                        // 发送所有输入
                        var result = NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
                        if (result == 0)
                        {
                            LoggerService.Instance.LogError($"SendInput 返回 0，可能被 UIPI 拦截 (请尝试以管理员身份运行)", "ClipboardService");
                            // 可以在这里弹个 Toast 提示用户，但暂时先只记录日志
                        }
                        else if (result < inputs.Length)
                        {
                            LoggerService.Instance.LogWarning($"SendInput 部分成功 ({result}/{inputs.Length})", "ClipboardService");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Instance.LogError($"SendInput 模拟失败: {ex.Message}", "ClipboardService");
                    }
                });

                // 3. 等待剪贴板更新
                await Task.Delay(200).ConfigureAwait(false); // 稍微增加等待时间

                // 4. 循环检测剪贴板 (最多 10 次，每次 100ms)
                for (int j = 0; j < 10; j++)
                {
                    try
                    {
                        string? text = null;
                        
                        // 必须在 UI 线程访问 Clipboard
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (FormsClipboard.ContainsText())
                            {
                                text = FormsClipboard.GetText();
                            }
                        });

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            LoggerService.Instance.LogInfo($"剪贴板方式成功（第{j+1}次尝试）: {text.Substring(0, Math.Min(50, text.Length))}...", "ClipboardService");
                            return text;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Instance.LogWarning($"剪贴板读取尝试{j+1}失败: {ex.Message}", "ClipboardService");
                    }

                    await Task.Delay(100).ConfigureAwait(false);
                }

                LoggerService.Instance.LogWarning("剪贴板取词超时，未检测到文本", "ClipboardService");
                return null;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"剪贴板方式获取文本失败: {ex.Message}", "ClipboardService");
                return null;
            }
        }

        /// <summary>
        /// 将文本设置到剪贴板
        /// </summary>
        public void SetClipboard(string text)
        {
            try
            {
                // 确保在 UI 线程执行
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        FormsClipboard.SetText(text);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Instance.LogError($"设置剪贴板失败内部错误: {ex.Message}", "ClipboardService.SetClipboard");
                    }
                });
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"设置剪贴板失败: {ex.Message}", "ClipboardService.SetClipboard");
            }
        }

        /// <summary>
        /// 模拟 Ctrl+V 将剪贴板内容粘贴到当前活跃窗口
        /// </summary>
        public void PasteToActiveWindow()
        {
            try
            {
                // 等待 100ms 确保窗口焦点已恢复
                Thread.Sleep(100);

                NativeMethods.keybd_event(VK_CONTROL, 0, 0, 0);
                NativeMethods.keybd_event(VK_V, 0, 0, 0);
                NativeMethods.keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
                NativeMethods.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"粘贴失败: {ex.Message}", "ClipboardService.PasteToActiveWindow");
            }
        }
    }
}
