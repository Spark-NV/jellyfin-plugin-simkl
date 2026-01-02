using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Jellyfin.Plugin.Simkl.Logging
{
    /// <summary>
    /// Custom logger for SIMKL plugin that writes to rotating log files.
    /// </summary>
    public class SimklLogger : IDisposable
    {
        private readonly string _logDirectory;
        private readonly string _baseLogFileName = "simkl_log";
        private readonly TimeSpan _maxLogAge = TimeSpan.FromHours(48);
        private readonly object _lock = new object();
        private readonly Timer _cleanupTimer;

        private string? _currentLogFile;
        private StreamWriter? _currentWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimklLogger"/> class.
        /// </summary>
        /// <param name="pluginDirectory">The plugin directory where log files will be stored.</param>
        public SimklLogger(string pluginDirectory)
        {
            _logDirectory = pluginDirectory;

            // Ensure log directory exists
            Directory.CreateDirectory(_logDirectory);

            // Rotate to new log file on startup
            RotateLogFile();

            // Set up cleanup timer to run every hour
            _cleanupTimer = new Timer(CleanupOldLogs, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Log an information message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="args">Optional arguments for string formatting.</param>
        public void LogInformation(string message, params object[] args)
        {
            Log("INFO", message, args);
        }

        /// <summary>
        /// Log a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="args">Optional arguments for string formatting.</param>
        public void LogWarning(string message, params object[] args)
        {
            Log("WARN", message, args);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="args">Optional arguments for string formatting.</param>
        public void LogError(string message, params object[] args)
        {
            Log("ERROR", message, args);
        }

        /// <summary>
        /// Log a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="args">Optional arguments for string formatting.</param>
        public void LogDebug(string message, params object[] args)
        {
            Log("DEBUG", message, args);
        }

        /// <summary>
        /// Log a message with exception details.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="args">Optional arguments for string formatting.</param>
        public void LogError(Exception exception, string message, params object[] args)
        {
            var formattedMessage = string.Format(message, args);
            Log("ERROR", $"{formattedMessage} - Exception: {exception.Message}\nStackTrace: {exception.StackTrace}");
        }

        private void Log(string level, string message, params object[] args)
        {
            lock (_lock)
            {
                try
                {
                    // Check if we need to rotate the log file (every 6 hours)
                    if (ShouldRotateLog())
                    {
                        RotateLogFile();
                    }

                    if (_currentWriter != null)
                    {
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        var formattedMessage = (args != null && args.Length > 0) ? string.Format(message, args) : message;
                        var logLine = $"[{timestamp}] [{level}] {formattedMessage}";

                        _currentWriter.WriteLine(logLine);
                        _currentWriter.Flush();
                    }
                }
                catch (Exception ex)
                {
                    // If logging fails, we don't want to throw exceptions that could break the application
                    // Just silently fail in this case
                    Console.WriteLine($"Failed to write to SIMKL log: {ex.Message}");
                }
            }
        }

        private bool ShouldRotateLog()
        {
            // Rotate log file every 6 hours
            if (_currentLogFile == null)
            {
                return true;
            }

            var fileName = Path.GetFileNameWithoutExtension(_currentLogFile);
            if (fileName != null && fileName.StartsWith(_baseLogFileName + "_"))
            {
                var timestampPart = fileName.Substring(_baseLogFileName.Length + 1);
                if (long.TryParse(timestampPart, out var epochTime))
                {
                    var fileTime = DateTimeOffset.FromUnixTimeSeconds(epochTime);
                    var currentTime = DateTimeOffset.Now;

                    // Rotate if more than 6 hours has passed
                    return (currentTime - fileTime).TotalHours >= 6;
                }
            }

            return true;
        }

        private void RotateLogFile()
        {
            lock (_lock)
            {
                // Close current writer
                _currentWriter?.Dispose();
                _currentWriter = null;

                // Create new log file with epoch timestamp
                var epochTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                var newLogFile = Path.Combine(_logDirectory, $"{_baseLogFileName}_{epochTime}.txt");

                try
                {
                    _currentWriter = new StreamWriter(newLogFile, false);
                    _currentLogFile = newLogFile;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create new SIMKL log file: {ex.Message}");
                }
            }
        }

        private void CleanupOldLogs(object? state)
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, $"{_baseLogFileName}_*.txt");

                foreach (var logFile in logFiles)
                {
                    try
                    {
                        // Don't delete the currently active log file
                        if (logFile == _currentLogFile)
                        {
                            continue;
                        }

                        var fileInfo = new FileInfo(logFile);
                        var fileName = Path.GetFileNameWithoutExtension(logFile);

                        if (fileName != null && fileName.StartsWith(_baseLogFileName + "_"))
                        {
                            var timestampPart = fileName.Substring(_baseLogFileName.Length + 1);
                            if (long.TryParse(timestampPart, out var epochTime))
                            {
                                var fileTime = DateTimeOffset.FromUnixTimeSeconds(epochTime);
                                var currentTime = DateTimeOffset.Now;

                                // Delete files older than 48 hours
                                if ((currentTime - fileTime).TotalHours > 48)
                                {
                                    File.Delete(logFile);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log cleanup errors to console since we can't use our own logger
                        Console.WriteLine($"Failed to cleanup SIMKL log file {logFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup SIMKL log files: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the logger and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _cleanupTimer?.Dispose();
                _currentWriter?.Dispose();
            }
        }
    }
}
