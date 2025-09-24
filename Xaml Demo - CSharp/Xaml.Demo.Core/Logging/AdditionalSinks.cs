using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Xaml_Demo.Logging
{
    /// <summary>
    /// Simple file log sink writing each message to a session log.
    /// Thread-safe via single lock. Creates (overwrites) file on first construction.
    /// </summary>
    public sealed class FileLogSink : ILogSink
    {
        private static readonly object _gate = new object();
        private readonly string _path;
        private bool _initialized;

        public FileLogSink(string? path = null)
        {
            _path = path ?? Path.Combine(Environment.CurrentDirectory, "session-log.txt");
        }

        public void Write(string message)
        {
            try
            {
                lock (_gate)
                {
                    if (!_initialized)
                    {
                        // Start fresh each run
                        File.WriteAllText(_path, $"# Session Log ({DateTime.Now:O}){Environment.NewLine}");
                        _initialized = true;
                    }

                    // Preserve raw message (LogRouter / callers may include timestamp already)
                    File.AppendAllText(_path, FormatLine(message));
                }
            }
            catch
            {
                // Swallow – logging must never throw.
            }
        }

        private static string FormatLine(string message)
        {
            // If caller already prepended a timestamp (common: [HH:MM:SS]) avoid duplication
            if (message.Length >= 10 && message[0] == '[' && message.IndexOf(']') > 5)
                return message + Environment.NewLine;

            return $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        }
    }

    /// <summary>
    /// Fan-out sink that writes to multiple underlying sinks.
    /// </summary>
    public sealed class CompositeLogSink : ILogSink
    {
        private readonly ILogSink[] _sinks;

        public CompositeLogSink(params ILogSink[] sinks)
        {
            _sinks = sinks ?? Array.Empty<ILogSink>();
        }

        public void Write(string message)
        {
            foreach (var s in _sinks)
            {
                try { s?.Write(message); } catch { /* ignore individual sink failures */ }
            }
        }
    }
}
