using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PromptMasterv5.Infrastructure.Services
{
    public class CommandExecutionService : ICommandExecutionService
    {
        private readonly ISettingsService _settingsService;
        private Dictionary<string, string> _commands = new();

        // Max edit distance allowed for fuzzy matching (per character ratio)
        private const double MaxEditDistanceRatio = 0.4; // Allow up to 40% character difference

        public event EventHandler? CommandsChanged;
        private FileSystemWatcher? _watcher;
        private DateTime _lastReadTime = DateTime.MinValue;

        public CommandExecutionService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadCommands();
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
            // Debounce
            var now = DateTime.Now;
            if ((now - _lastReadTime).TotalMilliseconds < 500) return;
            _lastReadTime = now;

            // Give the file a moment to release check locks
            System.Threading.Thread.Sleep(100);

            // Re-load
            LoadCommands();
            
            // Notify
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Returns all command keys for hotword generation.
        /// </summary>
        public IReadOnlyList<string> GetCommandKeys() => _commands.Keys.ToList();

        public Dictionary<string, string> GetCommands() => _commands;

        public void SetCommands(Dictionary<string, string> commands)
        {
            if (commands == null) return;
            _commands = commands;
            
            // Save to local file
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settingsService.Config.VoiceCommandConfigPath);
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(_commands, options);
                File.WriteAllText(configPath, json);
                LoggerService.Instance.LogInfo($"Saved {_commands.Count} voice commands to {configPath}", "CommandExecutionService.SetCommands");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save voice commands", "CommandExecutionService.SetCommands");
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
                    _commands = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
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

        public bool ExecuteCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var normalizedText = NormalizeText(text);

            LoggerService.Instance.LogInfo($"Processing voice command: '{text}' -> '{normalizedText}'", "CommandExecutionService.ExecuteCommand");

            // 1. Exact match
            if (_commands.TryGetValue(normalizedText, out var command))
            {
                LoggerService.Instance.LogInfo($"Exact match: '{normalizedText}'", "CommandExecutionService.ExecuteCommand");
                return ExecuteProcess(command);
            }

            // 2. Contains match
            var containsMatch = _commands.Keys.FirstOrDefault(k => normalizedText.Contains(NormalizeText(k)));
            if (containsMatch != null)
            {
                LoggerService.Instance.LogInfo($"Contains match: '{containsMatch}'", "CommandExecutionService.ExecuteCommand");
                return ExecuteProcess(_commands[containsMatch]);
            }

            // 3. Levenshtein fuzzy match
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
                    bestDistance = distance;
                    bestKey = key;
                }
            }

            if (bestKey != null)
            {
                LoggerService.Instance.LogInfo($"Fuzzy match: '{normalizedText}' -> '{bestKey}' (distance={bestDistance})", "CommandExecutionService.ExecuteCommand");
                return ExecuteProcess(_commands[bestKey]);
            }

            LoggerService.Instance.LogInfo($"No match found for: '{normalizedText}'", "CommandExecutionService.ExecuteCommand");
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

        /// <summary>
        /// Compute the Levenshtein edit distance between two strings.
        /// </summary>
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
    }
}
