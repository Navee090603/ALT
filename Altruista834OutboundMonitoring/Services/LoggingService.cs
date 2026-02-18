using System;
using System.IO;

namespace Altruista834OutboundMonitoring.Services
{
    public interface ILoggingService
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception ex = null);
    }

    public sealed class LoggingService : ILoggingService
    {
        private readonly object _sync = new object();
        private readonly string _logFile;

        public LoggingService(string logFile)
        {
            _logFile = string.IsNullOrWhiteSpace(logFile) ? "logs/monitor.log" : logFile;
            var directory = Path.GetDirectoryName(_logFile);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);

        public void Error(string message, Exception ex = null)
        {
            var full = ex == null ? message : $"{message} | Exception: {ex}";
            Write("ERROR", full);
        }

        private void Write(string level, string message)
        {
            var line = $"{DateTime.UtcNow:O} [{level}] {message}";
            lock (_sync)
            {
                Console.WriteLine(line);
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
        }
    }
}
