using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent; // 【新增】引入并发集合命名空间
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PromptMasterv5.Infrastructure.Services
{
    public class CommandExecutionService : ICommandExecutionService, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IAiService _aiService;

        // 【升级】全部使用线程安全的并发字典
        private ConcurrentDictionary<string, VoiceCommand> _commands = new();
        private ConcurrentDictionary<string, string> _intentCache = new();

        // 零容错指令关键词：仅限关机和重启类操作，其余指令均使用 40% 模糊容错。
        private static readonly string[] ZeroToleranceKeywords =
        {
            "shutdown", "reboot", "关机", "重启"
        };

        // 模糊匹配最大容错比例（40%），适用于非零容错指令
        private const double MaxEditDistanceRatio = 0.4;

        /// <summary>
        /// 判断某个指令是否属于"零容错"类别（关机/重启）。
        /// </summary>
        private bool IsDangerousCommand(VoiceCommand? command)
        {
            if (command == null || string.IsNullOrEmpty(command.ActionPath)) return false;
            var lower = command.ActionPath.ToLowerInvariant();

            foreach (var keyword in ZeroToleranceKeywords)
            {
                if (lower.Contains(keyword)) return true;
            }

            if (lower.Contains("shutdown.exe") || lower.StartsWith("shutdown "))
                return true;

            return false;
        }

        public event EventHandler? CommandsChanged;
        public event EventHandler? OnRoutingStarted;
        public event EventHandler? OnRoutingFinished;

        private FileSystemWatcher? _watcher;
        private DateTime _lastReadTime = DateTime.MinValue;

        public CommandExecutionService(ISettingsService settingsService, IAiService aiService)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            LoadCommands();
            LoadIntentCache();
            InitializeWatcher();
        }

        private void InitializeWatcher()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settingsService.Config.VoiceCommandConfigPath);
                var dir = Path.GetDirectoryName(configPath);
                var fileName = Path.GetFileName(configPath);

                if (Directory.Exists(dir))
                {
                    _watcher = new FileSystemWatcher(dir, fileName);
                    _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                    _watcher.Changed += OnFileChanged;
                    _watcher.Created += OnFileChanged;
                    _watcher.Renamed += OnFileChanged;
                    _watcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to initialize voice command file watcher", "CommandExecutionService.InitializeWatcher");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.Now;
            if ((now - _lastReadTime).TotalMilliseconds < 500) return;
            _lastReadTime = now;

            System.Threading.Thread.Sleep(100);

            LoadCommands();
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<string> GetCommandKeys() => _commands.Keys.ToList();

        // 【安全补丁】返回一个全新的普通字典副本，切断 UI 层与底层并发集合的直接联系
        public Dictionary<string, VoiceCommand> GetCommands() => new Dictionary<string, VoiceCommand>(_commands);

        public void SetCommands(Dictionary<string, VoiceCommand> commands)
        {
            if (commands == null) return;

            // 【升级】重新实例化为并发字典
            _commands = new ConcurrentDictionary<string, VoiceCommand>(commands);

            try
            {
                if (_watcher != null) _watcher.EnableRaisingEvents = false;

                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settingsService.Config.VoiceCommandConfigPath);

                // 【安全补丁】写入前确保上级目录存在，防止抛出 DirectoryNotFoundException
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(_commands, options);
                File.WriteAllText(configPath, json);
                LoggerService.Instance.LogInfo($"Saved {_commands.Count} voice commands to {configPath}", "CommandExecutionService.SetCommands");

                _lastReadTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save voice commands", "CommandExecutionService.SetCommands");
            }
            finally
            {
                if (_watcher != null) _watcher.EnableRaisingEvents = true;
            }
        }

        public void LoadCommands()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settingsService.Config.VoiceCommandConfigPath);
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);

                    try
                    {
                        // 【升级】先反序列化为普通字典，再装入并发字典，保证最高兼容性
                        var dict = JsonSerializer.Deserialize<Dictionary<string, VoiceCommand>>(json) ?? new Dictionary<string, VoiceCommand>();
                        _commands = new ConcurrentDictionary<string, VoiceCommand>(dict);
                    }
                    catch (JsonException)
                    {
                        LoggerService.Instance.LogInfo("Old format detected in voice_commands.json, migrating to new VoiceCommand format...", "CommandExecutionService.LoadCommands");
                        var legacyConfig = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

                        var newDict = new Dictionary<string, VoiceCommand>();
                        foreach (var kvp in legacyConfig)
                        {
                            newDict[kvp.Key] = new VoiceCommand
                            {
                                Name = kvp.Key,
                                ActionPath = kvp.Value,
                                Description = ""
                            };
                        }

                        SetCommands(newDict);
                    }

                    LoggerService.Instance.LogInfo($"Loaded {_commands.Count} voice commands", "CommandExecutionService.LoadCommands");
                }
                else
                {
                    LoggerService.Instance.LogWarning("Voice command config file not found", "CommandExecutionService.LoadCommands");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to load voice commands", "CommandExecutionService.LoadCommands");
            }
        }

        private void LoadIntentCache()
        {
            try
            {
                var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settingsService.Config.VoiceCommandCacheConfigPath);
                if (File.Exists(cachePath))
                {
                    var json = File.ReadAllText(cachePath);
                    // 【升级】先反序列化为普通字典，再装入并发字典
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                    _intentCache = new ConcurrentDictionary<string, string>(dict);

                    LoggerService.Instance.LogInfo($"Loaded {_intentCache.Count} cached intents", "CommandExecutionService.LoadIntentCache");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to load intent cache", "CommandExecutionService.LoadIntentCache");
            }
        }

        private void SaveIntentCache()
        {
            try
            {
                var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settingsService.Config.VoiceCommandCacheConfigPath);

                // 【安全补丁】写入前确保上级目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(_intentCache, options);
                File.WriteAllText(cachePath, json);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save intent cache", "CommandExecutionService.SaveIntentCache");
            }
        }

        public void ClearIntentCache()
        {
            _intentCache.Clear();
            SaveIntentCache();
            LoggerService.Instance.LogInfo("Intent cache cleared by user.", "CommandExecutionService.ClearIntentCache");
        }

        public async System.Threading.Tasks.Task<bool> ExecuteCommandAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var normalizedText = NormalizeText(text);
            LoggerService.Instance.LogInfo($"Processing voice command: '{text}' -> '{normalizedText}'", "CommandExecutionService.ExecuteCommandAsync");

            // --- 0. 极速通道：检查意图缓存 ---
            if (_intentCache.TryGetValue(normalizedText, out var cachedTargetName))
            {
                if (_commands.TryGetValue(cachedTargetName, out var targetCmd))
                {
                    LoggerService.Instance.LogInfo($"[Cache Match] Intent mapped from cache: '{normalizedText}' -> '{targetCmd.Name}'", "CommandExecutionService.ExecuteCommandAsync");
                    return ExecuteProcess(targetCmd.ActionPath);
                }
            }

            // --- 1. 极速通道：精确匹配 ---
            if (_commands.TryGetValue(normalizedText, out var command))
            {
                LoggerService.Instance.LogInfo($"[Exact Match] '{normalizedText}'", "CommandExecutionService.ExecuteCommandAsync");
                return ExecuteProcess(command.ActionPath);
            }

            // --- 2. 极速通道：包含匹配 ---
            var containsMatch = _commands.Keys.FirstOrDefault(k => normalizedText.Contains(NormalizeText(k)));
            if (containsMatch != null && _commands.TryGetValue(containsMatch, out var containsCmd))
            {
                LoggerService.Instance.LogInfo($"[Contains Match] '{containsMatch}'", "CommandExecutionService.ExecuteCommandAsync");
                return ExecuteProcess(containsCmd.ActionPath);
            }

            // --- 3. 极速通道：Levenshtein 模糊匹配 ---
            string? bestKey = null;
            int bestDistance = int.MaxValue;

            foreach (var key in _commands.Keys)
            {
                var normalizedKey = NormalizeText(key);
                int distance = LevenshteinDistance(normalizedText, normalizedKey);
                int maxLen = Math.Max(normalizedText.Length, normalizedKey.Length);
                double ratio = maxLen > 0 ? (double)distance / maxLen : 1.0;

                if (ratio <= MaxEditDistanceRatio && distance < bestDistance)
                {
                    if (distance > 0 && IsDangerousCommand(_commands[key]))
                    {
                        LoggerService.Instance.LogInfo($"Fuzzy match blocked for dangerous command: '{normalizedText}' -> '{key}' (distance={distance})", "CommandExecutionService.ExecuteCommandAsync");
                        continue;
                    }

                    bestDistance = distance;
                    bestKey = key;
                }
            }

            if (bestKey != null && _commands.TryGetValue(bestKey, out var bestCmd))
            {
                LoggerService.Instance.LogInfo($"[Fuzzy Match] '{normalizedText}' -> '{bestKey}' (distance={bestDistance})", "CommandExecutionService.ExecuteCommandAsync");
                return ExecuteProcess(bestCmd.ActionPath);
            }

            // --- 4. 智能兜底通道：呼叫大模型进行语义路由 ---
            LoggerService.Instance.LogInfo($"[Semantic Routing] Local match failed, requesting AI intent mapping for: '{normalizedText}'...", "CommandExecutionService.ExecuteCommandAsync");

            OnRoutingStarted?.Invoke(this, EventArgs.Empty);

            try
            {
                var commandListStr = string.Join("\n", _commands.Select(c => $"- [{c.Value.Name}]：{c.Value.Description}"));

                string systemPrompt = $@"你是一个底层操作系统的指令路由引擎。你的唯一任务是将用户的自然语言输入，映射到系统已注册的指令集上。

【已注册指令集】
{commandListStr}

【路由规则】
1. 分析用户的真实意图，并在上述指令集中寻找最匹配的一项。
2. 允许处理同音错别字、语气词、以及模糊描述（例如用户说“电脑好卡”，应映射到“清理文件”）。
3. 绝对严格的输出格式：只能输出指令的 Name（例如：清理文件）。
4. 绝对禁止输出任何标点符号、解释性文字或前缀。
5. 如果用户的输入纯粹是闲聊，或者完全不匹配任何指令，请严格输出单词：NONE";

                string aiResult = await _aiService.InterpretIntentAsync(systemPrompt, text, _settingsService.Config).ConfigureAwait(false);

                if (string.IsNullOrEmpty(aiResult) || aiResult.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                {
                    LoggerService.Instance.LogInfo($"[Semantic Routing] AI could not match intent (Result: {aiResult})", "CommandExecutionService.ExecuteCommandAsync");
                    return false;
                }

                if (_commands.TryGetValue(aiResult, out var aiMappedCmd))
                {
                    LoggerService.Instance.LogInfo($"[Semantic Routing] AI successfully mapped intent: '{normalizedText}' -> '{aiResult}'", "CommandExecutionService.ExecuteCommandAsync");

                    // 【线程安全】直接操作 ConcurrentDictionary 是绝对安全的
                    _intentCache[normalizedText] = aiResult;
                    SaveIntentCache();

                    return ExecuteProcess(aiMappedCmd.ActionPath);
                }
                else
                {
                    LoggerService.Instance.LogWarning($"[Semantic Routing] Hallucination detected: AI returned non-existent command '{aiResult}'", "CommandExecutionService.ExecuteCommandAsync");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "AI Semantic Routing failed", "CommandExecutionService.ExecuteCommandAsync");
                return false;
            }
            finally
            {
                OnRoutingFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 尝试精确匹配指令（用于抢答模式）
        /// 仅进行精确匹配和包含匹配，不进行模糊匹配
        /// </summary>
        public bool TryExactMatch(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var normalizedText = NormalizeText(text);

            // 【补丁修复】0. 极速通道：优先检查意图缓存！(必须享有最高优先级)
            if (_intentCache.TryGetValue(normalizedText, out var cachedTargetName))
            {
                if (_commands.TryGetValue(cachedTargetName, out var targetCmd))
                {
                    LoggerService.Instance.LogInfo($"[QuickMatch] Cache match: '{normalizedText}' -> '{targetCmd.Name}'", "CommandExecutionService.TryExactMatch");
                    return ExecuteProcess(targetCmd.ActionPath);
                }
            }

            // 1. Exact match
            if (_commands.TryGetValue(normalizedText, out var command))
            {
                LoggerService.Instance.LogInfo($"[QuickMatch] Exact match: '{normalizedText}'", "CommandExecutionService.TryExactMatch");
                return ExecuteProcess(command.ActionPath);
            }

            // 2. Contains match
            var containsMatch = _commands.Keys.FirstOrDefault(k => normalizedText.Contains(NormalizeText(k)));
            if (containsMatch != null && _commands.TryGetValue(containsMatch, out var containsCmd))
            {
                LoggerService.Instance.LogInfo($"[QuickMatch] Contains match: '{containsMatch}'", "CommandExecutionService.TryExactMatch");
                return ExecuteProcess(containsCmd.ActionPath);
            }

            return false;
        }

        private bool ExecuteProcess(string command)
        {
            try
            {
                ProcessStartInfo psi;

                if (command.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-ExecutionPolicy Bypass -File \"{command}\"",
                        UseShellExecute = false
                    };
                }
                else
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = command,
                        UseShellExecute = true
                    };
                }

                Process.Start(psi);
                LoggerService.Instance.LogInfo($"Executed voice command: {command}", "CommandExecutionService.ExecuteProcess");
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Failed to execute command: {command}", "CommandExecutionService.ExecuteProcess");
                return false;
            }
        }

        private string NormalizeText(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return new string(input.Where(c => !char.IsPunctuation(c)).ToArray()).ToLowerInvariant().Trim();
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        // ========== IDisposable ==========
        private bool _disposed = false;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Renamed -= OnFileChanged;
                _watcher.Dispose();
                _watcher = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}