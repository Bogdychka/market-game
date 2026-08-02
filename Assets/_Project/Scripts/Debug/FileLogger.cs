using System;
using System.IO;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Writes all Debug logs to a file at the project root (game.log).
    /// Initialized once from GameBootstrap.
    /// </summary>
    public static class FileLogger
    {
        private static StreamWriter _writer;
        private static bool _initialized;
        private static string _logPath;

        public static string LogPath => _logPath;

        /// <summary>
        /// Play mode runs without a domain reload, so statics survive between sessions. If the
        /// previous session ended without <see cref="Shutdown"/>, <c>_initialized</c> would still
        /// be true and <see cref="Initialize"/> would return early, leaving the run unlogged.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Application.logMessageReceived -= OnLog;
            Application.quitting -= Shutdown;

            try
            {
                _writer?.Close();
            }
            catch (Exception)
            {
                // The previous session may already have closed it; nothing useful to report.
            }

            _writer = null;
            _initialized = false;
            _logPath = null;
        }

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Project root (directory that contains the Assets folder)
                _logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "game.log"));

                _writer = new StreamWriter(_logPath, append: false);
                // Do not flush on every routine Log (audit L6); severe messages and Shutdown flush
                // explicitly, so a crash still captures the important lines without per-line disk I/O.
                _writer.AutoFlush = false;

                Application.logMessageReceived += OnLog;
                Application.quitting += Shutdown;

                _initialized = true;

                _writer.WriteLine($"=== FileLogger started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                Debug.Log($"[FileLogger] Writing logs to: {_logPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileLogger] Failed to open log file: {e.Message}");
            }
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (_writer == null) return;

            string prefix = type switch
            {
                LogType.Error     => "ERR ",
                LogType.Warning   => "WARN",
                LogType.Exception => "EXC ",
                LogType.Assert    => "ASRT",
                _                 => "LOG "
            };

            _writer.WriteLine($"[{Time.realtimeSinceStartup:F2}] [{prefix}] {message}");

            if (type == LogType.Error || type == LogType.Exception)
                _writer.WriteLine(stackTrace);

            // Flush the buffer on anything that might precede a crash, so important lines survive.
            if (type != LogType.Log)
                _writer.Flush();
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            Application.logMessageReceived -= OnLog;
            _writer?.WriteLine($"=== FileLogger stopped at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _writer?.Close();
            _writer = null;
            _initialized = false;
        }
    }
}
