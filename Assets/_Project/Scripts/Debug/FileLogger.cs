using System;
using System.IO;
using UnityEngine;

namespace Market.DebugTools
{
    /// <summary>
    /// Пишет все Debug-логи в файл в корне проекта (game.log).
    /// Инициализируется один раз из GameBootstrap.
    /// </summary>
    public static class FileLogger
    {
        private static StreamWriter _writer;
        private static bool _initialized;
        private static string _logPath;

        public static string LogPath => _logPath;

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Корень проекта (где лежит папка Assets)
                _logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "game.log"));

                _writer = new StreamWriter(_logPath, append: false);
                _writer.AutoFlush = true;

                Application.logMessageReceived += OnLog;
                Application.quitting += Shutdown;

                _initialized = true;

                _writer.WriteLine($"=== FileLogger started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                Debug.Log($"[FileLogger] Пишу логи в: {_logPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileLogger] Не удалось открыть файл: {e.Message}");
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
