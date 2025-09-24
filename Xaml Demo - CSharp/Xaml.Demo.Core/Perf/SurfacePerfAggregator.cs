using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Xaml_Demo.Logging;
using Xaml_Demo.Surfaces;

namespace Xaml_Demo.Perf
{
    /// <summary>
    /// Realization performance aggregation (early window + tail diagnostics) per surface.
    ///
    /// Concepts:
    /// - First-N Window: Exact first TargetCount UNIQUE container realizations (deduped by containerId).
    ///   Percentiles (p50/p90/p99), avg, min, max computed ONLY over this window to keep
    ///   early readiness metrics stable and comparable across runs.
    /// - Duplicates: Repeated realization events for the same containerId (re-binds, reuse).
    ///   Counted separately (DuplicateCount) – excluded from samples.
    /// - Tail (Overflow): UNIQUE container realizations AFTER the first-N window is closed.
    ///   Aggregated (TailCount / TailMin / TailMax / TailLastElapsed). Not included in percentiles.
    /// - Session (Correlation): Each Start(kind) increments SessionId. Late events from an
    ///   earlier session are ignored (and optionally logged once).
    /// - Summary: Emitted immediately when the first-N window closes (Readiness summary).
    ///   A later HardStop(kind, reason) optionally emits a Survey including tail stats.
    ///
    /// Thread-safety: Internal locking per static dictionary. Sorting performed outside lock.
    /// </summary>
    public static class SurfacePerfAggregator
    {
        private sealed class SurfaceState
        {
            public readonly Stopwatch Stopwatch = new Stopwatch();

            // Configuration / session
            public int TargetCount;
            public int SessionId;

            // First-N window data
            public double[] Samples = Array.Empty<double>();
            public int Count;              // unique samples within window
            public double Total;
            public double Min = double.MaxValue;
            public double Max = double.MinValue;

            // Tail data (unique after window)
            public int TailCount;
            public double TailMin = double.MaxValue;
            public double TailMax = double.MinValue;
            public double TailLastElapsed;

            // Duplicate data
            public int DuplicateCount;

            // Identity tracking
            public HashSet<long> SeenContainers = new HashSet<long>();
            public bool WindowClosed;          // first-N window complete (summary emitted)
            public bool SummaryEmitted;        // readiness summary emitted
            public bool OverflowLogged;        // first overflow line emitted
            public bool StaleLogged;           // stale session warning emitted
            public long FallbackCounter;       // synthetic container ids for legacy calls

            // Completion (hard stop / flush)
            public bool Completed;             // no further recording accepted after completion
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<FrameworkSurfaceKind, SurfaceState> _states = new Dictionary<FrameworkSurfaceKind, SurfaceState>();

        #region Public API

        /// <summary>Begin (or restart) tracking for a surface. Returns new SessionId.</summary>
        public static int Start(FrameworkSurfaceKind kind, int targetCount)
        {
            if (targetCount <= 0) targetCount = 1;
            SurfaceState state;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out state))
                {
                    state = new SurfaceState();
                    _states[kind] = state;
                }

                state.SessionId++;
                state.TargetCount = targetCount;
                state.Count = 0;
                state.Total = 0;
                state.Min = double.MaxValue;
                state.Max = double.MinValue;

                state.TailCount = 0;
                state.TailMin = double.MaxValue;
                state.TailMax = double.MinValue;
                state.TailLastElapsed = 0;

                state.DuplicateCount = 0;
                state.SeenContainers.Clear();

                state.WindowClosed = false;
                state.SummaryEmitted = false;
                state.OverflowLogged = false;
                state.StaleLogged = false;
                state.Completed = false;

                if (state.Samples.Length != targetCount)
                    state.Samples = new double[targetCount];

                state.Stopwatch.Restart();
            }

            LogRouter.Write($"PERF[SurfaceRealizationStart:{kind}]: session={state.SessionId} target={targetCount}");
            return state.SessionId;
        }

        /// <summary>
        /// Legacy recording (no container identity). Generates synthetic monotonic containerId.
        /// Duplicates cannot be distinguished with this path.
        /// </summary>
        public static void RecordRealized(FrameworkSurfaceKind kind)
        {
            SurfaceState? state;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out state) || state.Completed)
                    return;

                // synthetic id
                long cid = ++state.FallbackCounter;
                int session = state.SessionId;
                // Defer outside lock
                // We release lock and call overload (which will re-lock) to unify logic.
            }
            // Recursion safe: will re-check Completed
            RecordRealized(kind, containerId: 0, source: "Legacy");
        }

        /// <summary>
        /// Record a realization with a stable container identity.
        /// containerId should be stable per visual container instance (e.g. RuntimeHelpers.GetHashCode(view) or native pointer).
        /// </summary>
        public static void RecordRealized(FrameworkSurfaceKind kind, long containerId, int? sessionId = null, string? source = null)
        {
            SurfaceState? state;
            double elapsed;
            int target;
            bool emitSummary = false;
            bool windowClosedNow = false;
            bool completed;

            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out state) || state.Completed)
                    return;

                if (sessionId.HasValue && sessionId.Value != state.SessionId)
                {
                    if (!state.StaleLogged)
                    {
                        LogRouter.Write($"PERF[SurfaceRealizationStale:{kind}]: eventSession={sessionId.Value} currentSession={state.SessionId}");
                        state.StaleLogged = true;
                    }
                    return;
                }

                elapsed = state.Stopwatch.Elapsed.TotalMilliseconds;
                target = state.TargetCount;

                // Window not yet closed
                if (!state.WindowClosed)
                {
                    // Deduplicate
                    if (!state.SeenContainers.Add(containerId))
                    {
                        state.DuplicateCount++;
                        return;
                    }

                    // Add sample
                    int index = state.Count;
                    if (index < target)
                    {
                        state.Samples[index] = elapsed;
                        state.Count++;
                        state.Total += elapsed;
                        if (elapsed < state.Min) state.Min = elapsed;
                        if (elapsed > state.Max) state.Max = elapsed;

                        LogRouter.Write($"PERF[SurfaceRealization:{kind}:{state.Count}]: {elapsed:N2} ms{FormatSource(source)}");

                        if (state.Count >= target)
                        {
                            state.WindowClosed = true;
                            windowClosedNow = true;
                            emitSummary = true; // readiness summary
                        }
                    }
                    else
                    {
                        // Safety (should not happen because we flip WindowClosed when Count==target)
                        state.WindowClosed = true;
                        windowClosedNow = true;
                        emitSummary = true;
                    }
                }
                else
                {
                    // Tail (unique after window)
                    if (!state.SeenContainers.Add(containerId))
                    {
                        state.DuplicateCount++;
                        return;
                    }

                    state.TailCount++;
                    if (elapsed < state.TailMin) state.TailMin = elapsed;
                    if (elapsed > state.TailMax) state.TailMax = elapsed;
                    state.TailLastElapsed = elapsed;

                    if (!state.OverflowLogged)
                    {
                        LogRouter.Write($"PERF[SurfaceRealizationOverflowStart:{kind}]: at={elapsed:N2} ms unique={state.Count}");
                        state.OverflowLogged = true;
                    }
                    // Per-tail realization (optional, keep concise):
                    LogRouter.Write($"PERF[SurfaceRealizationTail:{kind}:{state.TailCount}]: {elapsed:N2} ms{FormatSource(source)}");
                }

                completed = state.Completed;
            }

            if (emitSummary)
            {
                EmitSummary(kind, readinessPhase: true);
            }

            // A "windowClosedNow" but not Completed indicates we keep tail metrics gathering.
        }

        /// <summary>
        /// Force early readiness summary if window is still open (partial) or emit survey if closed.
        /// Equivalent to a lifecycle flush where the host knows the view is unloading.
        /// </summary>
        public static void Flush(FrameworkSurfaceKind kind)
        {
            bool doSummary = false;
            bool finalSurvey = false;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out var state) || state.Completed)
                    return;

                if (!state.WindowClosed)
                {
                    // Partial window summary
                    doSummary = true;
                }
                else
                {
                    // Tail may have accumulated; produce final survey
                    finalSurvey = true;
                }

                state.Completed = true;
            }

            if (doSummary)
            {
                EmitSummary(kind, readinessPhase: true); // partial readiness snapshot
            }
            if (finalSurvey)
            {
                EmitSurvey(kind, reason: "Flush");
            }
        }

        /// <summary>
        /// Hard stop with reason: emits survey (even if partial) then marks Completed.
        /// </summary>
        public static void HardStop(FrameworkSurfaceKind kind, string reason)
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
                EmitSurvey(kind, reason);
            }
        }

        /// <summary>Get current session id for a surface (0 if not started).</summary>
        public static int GetCurrentSessionId(FrameworkSurfaceKind kind)
        {
            lock (_lock)
            {
                if (_states.TryGetValue(kind, out var state))
                    return state.SessionId;
                return 0;
            }
        }

        #endregion

        #region Internal Emission

        private static void EmitSummary(FrameworkSurfaceKind kind, bool readinessPhase)
        {
            SurfaceState? state;
            int count;
            int target;
            double min, max, total;
            double[] sampleCopy;
            int duplicateCount;
            int tailCount;
            bool windowClosed;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out state))
                    return;

                // Avoid duplicate readiness summaries
                if (readinessPhase && state.SummaryEmitted)
                    return;

                count = state.Count;
                target = state.TargetCount;
                min = state.Min == double.MaxValue ? 0 : state.Min;
                max = state.Max == double.MinValue ? 0 : state.Max;
                total = state.Total;
                duplicateCount = state.DuplicateCount;
                tailCount = state.TailCount;
                windowClosed = state.WindowClosed;

                sampleCopy = new double[count];
                Array.Copy(state.Samples, sampleCopy, count);

                if (readinessPhase)
                    state.SummaryEmitted = true;
            }

            double avg = count > 0 ? total / count : 0;

            double p50 = 0, p90 = 0, p99 = 0;
            if (count > 0)
            {
                Array.Sort(sampleCopy);
                p50 = GetPercentile(sampleCopy, 0.50);
                p90 = GetPercentile(sampleCopy, 0.90);
                p99 = GetPercentile(sampleCopy, 0.99);
            }

            string phase = readinessPhase
                ? (windowClosed ? "Readiness" : "Partial")
                : "Survey";

            string readiness = ClassifyReadiness(windowClosed, tailCount, state: null); // state not needed here

            LogRouter.Write(
                $"PERF[SurfaceRealizationSummary:{kind}]: phase={phase} session={state?.SessionId} count={count} target={target} dup={duplicateCount} tail={tailCount} avg={avg:N2} ms min={min:N2} ms p50={p50:N2} ms p90={p90:N2} ms p99={p99:N2} ms max={max:N2} ms readiness={readiness}");
        }

        private static void EmitSurvey(FrameworkSurfaceKind kind, string reason)
        {
            SurfaceState? state;
            int count;
            int target;
            int dup;
            int tailCount;
            double tailMin, tailMax, tailLast;
            bool windowClosed;
            lock (_lock)
            {
                if (!_states.TryGetValue(kind, out state))
                    return;
                count = state.Count;
                target = state.TargetCount;
                dup = state.DuplicateCount;
                tailCount = state.TailCount;
                tailMin = state.TailMin == double.MaxValue ? 0 : state.TailMin;
                tailMax = state.TailMax == double.MinValue ? 0 : state.TailMax;
                tailLast = state.TailLastElapsed;
                windowClosed = state.WindowClosed;
            }

            string readiness = ClassifyReadiness(windowClosed, tailCount, state);
            string status = windowClosed ? "WindowClosed" : "PartialWindow";

            LogRouter.Write(
                $"PERF[SurfaceRealizationSurvey:{kind}]: reason={reason} session={state?.SessionId} status={status} unique={count} target={target} dup={dup} tail={tailCount} tailMin={tailMin:N2} ms tailMax={tailMax:N2} ms tailLast={tailLast:N2} ms readiness={readiness}");
        }

        private static string ClassifyReadiness(bool windowClosed, int tailCount, SurfaceState? state)
        {
            if (!windowClosed)
                return "NotReady";

            if (tailCount == 0)
                return "Ready";

            // Window closed, some tail activity
            if (state == null)
                return "ReadyWithTail";

            // Heuristic: large tail relative to window size -> UnstableTail
            if (tailCount >= Math.Max(3, state.TargetCount / 2))
                return "UnstableTail";

            return "ReadyWithTail";
        }

        private static double GetPercentile(double[] sorted, double p)
        {
            if (sorted.Length == 0) return 0;
            if (sorted.Length == 1) return sorted[0];
            double pos = p * (sorted.Length - 1);
            int idx = (int)Math.Round(pos);
            if (idx < 0) idx = 0;
            if (idx >= sorted.Length) idx = sorted.Length - 1;
            return sorted[idx];
        }

        private static string FormatSource(string? source)
            => source is null ? string.Empty : $" src={source}";

        #endregion
    }

    /// <summary>
    /// Centralized perf configuration knobs (modifiable at runtime if desired).
    /// </summary>
    public static class PerfConfig
    {
        /// <summary>Number of first realized UNIQUE items to sample per surface before readiness summary.</summary>
        public static int FirstRealizationSampleCount { get; set; } = 50;
    }
}
