using System;

namespace Xaml_Demo.Logging
{
    /// <summary>
    /// Static indirection point so Core ViewModels can log without referencing
    /// platform-specific implementations. The MAUI layer (or future WPF host)
    /// assigns <see cref="Sink"/> at startup.
    /// </summary>
    public static class LogRouter
    {
        public static ILogSink? Sink { get; set; }

        public static void Write(string message)
        {
            try
            {
                Sink?.Write(message);
            }
            catch
            {
                // Swallow: logging must never throw into app logic.
            }
        }
    }
}
