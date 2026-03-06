using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PromptMasterv6.Infrastructure.Services
{
    /// <summary>
    /// Provides thread-safe logging functionality with automatic log rotation
    /// </summary>
    public class LoggerService
    {
        private static readonly Lazy<LoggerService> _instance = new Lazy<LoggerService>(() => new LoggerService());
        private readonly string _logDirectory;
        private readonly string _logFileName;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private const long MaxLogFileSize = 5 * 1024 * 1024; // 5MB

        public static LoggerService Instance => _instance.Value;

        private LoggerService()
        {
            // Store logs in application directory/Logs
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            _logDirectory = Path.Combine(appPath, "Logs");
            _logFileName = "app.log";

            // Create log directory if it doesn't exist
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
                // If we can't create the log directory, we'll fail silently
                // to prevent the app from crashing
            }
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        public void LogInfo(string message, string? source = null)
        {
            WriteLog("INFO", message, source);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public void LogWarning(string message, string? source = null)
        {
            WriteLog("WARN", message, source);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public void LogError(string message, string? source = null)
        {
            WriteLog("ERROR", message, source);
        }

        /// <summary>
        /// Logs an exception with full details
        /// </summary>
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

            // Log inner exceptions
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

        /// <summary>
        /// Logs a debug message (only in debug builds)
        /// </summary>
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

                    // Check if log rotation is needed
                    if (File.Exists(logFilePath))
                    {
                        var fileInfo = new FileInfo(logFilePath);
                        if (fileInfo.Length > MaxLogFileSize)
                        {
                            RotateLogFile(logFilePath);
                        }
                    }

                    // Format log entry
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    var sourceInfo = string.IsNullOrEmpty(source) ? "" : $" [{source}]";
                    var logEntry = $"[{timestamp}] [{level}] [Thread-{threadId}]{sourceInfo} {message}";

                    // Write to file
                    File.AppendAllText(logFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch
            {
                // Fail silently to prevent logging from crashing the app
            }
        }

        private void RotateLogFile(string currentLogPath)
        {
            try
            {
                // Keep up to 5 backup files
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

                // Move current log to .1
                var firstBackup = Path.Combine(_logDirectory, "app.log.1");
                if (File.Exists(firstBackup))
                {
                    File.Delete(firstBackup);
                }
                File.Move(currentLogPath, firstBackup);
            }
            catch
            {
                // If rotation fails, just continue - the file will be overwritten
            }
        }

        /// <summary>
        /// Gets the path to the current log file
        /// </summary>
        public string GetLogFilePath()
        {
            return Path.Combine(_logDirectory, _logFileName);
        }

        /// <summary>
        /// Gets the log directory path
        /// </summary>
        public string GetLogDirectory()
        {
            return _logDirectory;
        }

        /// <summary>
        /// Clears all log files
        /// </summary>
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
                                // Continue even if some files can't be deleted
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
                // Fail silently
            }
        }
    }
}
