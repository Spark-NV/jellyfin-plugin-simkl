using System;
using System.IO;

namespace Jellyfin.Plugin.Simkl.Logging
{
    /// <summary>
    /// Factory for creating and managing SIMKL loggers.
    /// </summary>
    public static class SimklLoggerFactory
    {
        private static readonly object _lock = new object();
        private static SimklLogger? _logger;

        /// <summary>
        /// Gets the shared SIMKL logger instance.
        /// </summary>
        public static SimklLogger Instance
        {
            get
            {
                if (_logger == null)
                {
                    lock (_lock)
                    {
                        if (_logger == null)
                        {
                            var pluginDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                            if (pluginDirectory != null)
                            {
                                _logger = new SimklLogger(pluginDirectory);
                            }
                            else
                            {
                                throw new InvalidOperationException("Cannot determine plugin directory for logging.");
                            }
                        }
                    }
                }

                return _logger;
            }
        }

        /// <summary>
        /// Disposes the shared logger instance.
        /// </summary>
        public static void Dispose()
        {
            lock (_lock)
            {
                _logger?.Dispose();
                _logger = null;
            }
        }
    }
}
