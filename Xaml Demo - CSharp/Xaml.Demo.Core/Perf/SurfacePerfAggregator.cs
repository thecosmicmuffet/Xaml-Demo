using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xaml_Demo.Logging;
using Xaml_Demo.Surfaces;

namespace Xaml_Demo.Perf
{
    /// <summary>
    /// Captures first-N item realization timings per surface (e.g., MAUI CollectionView vs WinUI ListView)
    /// and emits incremental + summary PERF log lines via LogRouter.
    /// Thread-safe; lightweight locking around per-surface state.
    /// </summary>
    public static class SurfacePerfAggregator
    {
        private sealed class SurfaceState
        {
public readonly Stopwatch Stopwatch = new Stopwatch();
            public int TargetCount;
            public int Count;
            public double Total;
            public double Min = double.MaxValue;
            public double Max = double.MinValue;
            public bool Completed;
        }

private static readonly object _lock = new object();
private static readonly Dictionary<FrameworkSurfaceKind, SurfaceState> _states = new Dictionary<FrameworkSurfaceKind, SurfaceState>();

        /// <summary>
        /// Begin tracking a surface. If already tracking and not completed, resets the state.
        /// </summary>
        public static void Start(FrameworkSurfaceKind kind, int targetCount)
        {
            if (targetCount <= 0) targetCount = 1;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out var state))
                {
                    state = new SurfaceState();
                    _states[kind] = state;
                }
                state.TargetCount = targetCount;
                state.Count = 0;
                state.Total = 0;
                state.Min = double.MaxValue;
                state.Max = double.MinValue;
                state.Completed = false;
                state.Stopwatch.Restart();
            }
            LogRouter.Write($"PERF[SurfaceRealizationStart:{kind}]: target={targetCount}");
        }

        /// <summary>
        /// Record a single item realization for the given surface.
        /// Safe to call from any thread. No-op if surface not started or already completed.
        /// </summary>
        public static void RecordRealized(FrameworkSurfaceKind kind)
        {
            SurfaceState? state;
            double elapsed;
            int current;
            int target;
            bool completeNow = false;

            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out state) || state.Completed)
                    return;

                elapsed = state.Stopwatch.Elapsed.TotalMilliseconds;
                state.Count++;
                state.Total += elapsed;
                if (elapsed < state.Min) state.Min = elapsed;
                if (elapsed > state.Max) state.Max = elapsed;

                current = state.Count;
                target = state.TargetCount;

                // Emit incremental log (each realization) up to target.
                LogRouter.Write($"PERF[SurfaceRealization:{kind}:{current}]: {elapsed:N2} ms");

                if (current >= target)
                {
                    state.Completed = true;
                    completeNow = true;
                }
            }

            if (completeNow)
            {
                EmitSummary(kind);
            }
        }

        /// <summary>
        /// Force summary emission (e.g., on view unload) even if target not reached.
        /// </summary>
        public static void Flush(FrameworkSurfaceKind kind)
        {
            bool emit;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out var state) || state.Completed)
                    return;
                state.Completed = true;
                emit = true;
            }
            if (emit)
            {
                EmitSummary(kind);
            }
        }

        private static void EmitSummary(FrameworkSurfaceKind kind)
        {
            double total;
            double min;
            double max;
            int count;
            int target;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out var state))
                    return;
                total = state.Total;
                min = state.Min == double.MaxValue ? 0 : state.Min;
                max = state.Max == double.MinValue ? 0 : state.Max;
                count = state.Count;
                target = state.TargetCount;
            }

            double avg = count > 0 ? total / count : 0;
            LogRouter.Write($"PERF[SurfaceRealizationSummary:{kind}]: count={count} target={target} avg={avg:N2} ms min={min:N2} ms max={max:N2} ms");
        }
    }

    /// <summary>
    /// Centralized perf configuration knobs (modifiable at runtime if desired).
    /// </summary>
    public static class PerfConfig
    {
        /// <summary>
        /// Number of first realized items to sample per surface before summary.
        /// </summary>
        public static int FirstRealizationSampleCount { get; set; } = 50;
    }
}
