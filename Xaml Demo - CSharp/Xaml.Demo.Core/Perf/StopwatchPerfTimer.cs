using System;
using System.Diagnostics;
using Xaml_Demo.Logging;

namespace Xaml_Demo.Perf
{
    /// <summary>
    /// Stopwatch-based implementation of IPerfTimer with automatic logging on Stop().
    /// Intended for lightweight, ad-hoc instrumentation of view / surface operations.
    /// Usage pattern:
    ///   var timer = new StopwatchPerfTimer();
    ///   timer.Start("SurfaceInit");
    ///   ... work ...
    ///   timer.Stop(); // Logs PERF[SurfaceInit]: <ms> ms
    /// Multiple starts overwrite the previous scope and restart timing.
    /// </summary>
    public sealed class StopwatchPerfTimer : IPerfTimer
    {
        private readonly Stopwatch _sw = new Stopwatch();
        private string? _scopeName;

        public double ElapsedMilliseconds => _sw.Elapsed.TotalMilliseconds;

        public void Start(string scopeName)
        {
            _scopeName = scopeName ?? string.Empty;
            _sw.Restart();
        }

        public void Stop()
        {
            if (!_sw.IsRunning)
                return;

            _sw.Stop();
            if (!string.IsNullOrEmpty(_scopeName))
            {
                // Centralized logging; dispatching will be handled by LogRouter.
                LogRouter.Write($"PERF[{_scopeName}]: {_sw.Elapsed.TotalMilliseconds:N2} ms");
            }
            _scopeName = null;
        }
    }
}
