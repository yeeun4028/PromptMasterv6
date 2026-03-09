using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PromptMasterv6.Infrastructure.Services
{
    public class LoggerService
    {
        private static readonly Lazy<LoggerService> _instance = new Lazy<LoggerService>(() => new LoggerService());
        private readonly string _logDirectory;
        private readonly string _logFileName;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private const long MaxLogFileSize = 5 * 1024 * 1024;

        public static LoggerService Instance => _instance.Value;

        public LoggerService()
        {
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            _logDirectory = Path.Combine(appPath, "Logs");
            _logFileName = "app.log";

            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
            }
        }

        public void LogInfo(string message, string? source = null)
        {
            WriteLog("INFO", message, source);
        }

        public void LogWarning(string message, string? source = null)
        {
            WriteLog("WARN", message, source);
        }

        public void LogError(string message, string? source = null)
        {
            WriteLog("ERROR", message, source);
        }

        public void LogException(Exception ex, string? additionalInfo = null, string? source = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Exception: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                sb.AppendLine($"Additional Info: {additionalInfo}");
            }

            sb.AppendLine($"Stack Trace: {ex.StackTrace}");

            var innerEx = ex.InnerException;
            int depth = 1;
            while (innerEx != null && depth <= 5)
            {
                sb.AppendLine($"Inner Exception [{depth}]: {innerEx.GetType().Name}");
                sb.AppendLine($"Message: {innerEx.Message}");
                sb.AppendLine($"Stack Trace: {innerEx.StackTrace}");
                innerEx = innerEx.InnerException;
                depth++;
            }

            WriteLog("ERROR", sb.ToString(), source);
        }

        public void LogDebug(string message, string? source = null)
        {
#if DEBUG
            WriteLog("DEBUG", message, source);
#endif
        }

        private void WriteLog(string level, string message, string? source)
        {
            try
            {
                _semaphore.Wait();
                try
                {
                    var logFilePath = Path.Combine(_logDirectory, _logFileName);

                    if (File.Exists(logFilePath))
                    {
                        var fileInfo = new FileInfo(logFilePath);
                        if (fileInfo.Length > MaxLogFileSize)
                        {
                            RotateLogFile(logFilePath);
                        }
                    }

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    var sourceInfo = string.IsNullOrEmpty(source) ? "" : $" [{source}]";
                    var logEntry = $"[{timestamp}] [{level}] [Thread-{threadId}]{sourceInfo} {message}";

                    File.AppendAllText(logFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch
            {
            }
        }

        private void RotateLogFile(string currentLogPath)
        {
            try
            {
                for (int i = 4; i >= 1; i--)
                {
                    var oldBackup = Path.Combine(_logDirectory, $"app.log.{i}");
                    var newBackup = Path.Combine(_logDirectory, $"app.log.{i + 1}");

                    if (File.Exists(oldBackup))
                    {
                        if (File.Exists(newBackup))
                        {
                            File.Delete(newBackup);
                        }
                        File.Move(oldBackup, newBackup);
                    }
                }

                var firstBackup = Path.Combine(_logDirectory, "app.log.1");
                if (File.Exists(firstBackup))
                {
                    File.Delete(firstBackup);
                }
                File.Move(currentLogPath, firstBackup);
            }
            catch
            {
            }
        }

        public string GetLogFilePath()
        {
            return Path.Combine(_logDirectory, _logFileName);
        }

        public string GetLogDirectory()
        {
            return _logDirectory;
        }

        public void ClearLogs()
        {
            try
            {
                _semaphore.Wait();
                try
                {
                    if (Directory.Exists(_logDirectory))
                    {
                        foreach (var file in Directory.GetFiles(_logDirectory, "*.log*"))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch
            {
            }
        }
    }
}
