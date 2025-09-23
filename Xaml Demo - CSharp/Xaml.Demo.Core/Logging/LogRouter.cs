using System;
using Xaml_Demo.Dispatching;

namespace Xaml_Demo.Logging
{
    /// <summary>
    /// Static indirection point so Core ViewModels can log without referencing
    /// platform-specific implementations. The platform layer assigns <see cref="Sink"/> at startup.
    /// Added optional dispatcher marshaling so background / cross-surface threads can log safely.
    /// </summary>
    public static class LogRouter
    {
        /// <summary>
        /// Assigned by host (MAUI / WPF / etc.) to receive log messages.
        /// </summary>
        public static ILogSink? Sink { get; set; }

        /// <summary>
        /// If true (default), Write() will attempt to marshal to the UI dispatcher when required.
        /// Disable if the sink itself is thread-safe or already marshals.
        /// </summary>
        public static bool EnableDispatcherMarshaling { get; set; } = true;

        /// <summary>
        /// Optional explicit dispatcher override. If null, falls back to UiDispatcherAmbient.Current.
        /// </summary>
        public static IUiDispatcher? DispatcherOverride { get; set; }

        private static IUiDispatcher? EffectiveDispatcher =>
            DispatcherOverride ?? UiDispatcherAmbient.Current;

        public static void Write(string message)
        {
            try
            {
                if (Sink == null)
                    return;

                if (EnableDispatcherMarshaling)
                {
                    var d = EffectiveDispatcher;
                    if (d != null && d.IsDispatchRequired)
                    {
                        // Fire-and-forget marshal
                        d.Post(() => SafeWrite(message));
                        return;
                    }
                }

                SafeWrite(message);
            }
            catch
            {
                // Swallow: logging must never throw into app logic.
            }
        }

        private static void SafeWrite(string message)
        {
            try
            {
                Sink?.Write(message);
            }
            catch
            {
                // Final safety net.
            }
        }
    }
}
