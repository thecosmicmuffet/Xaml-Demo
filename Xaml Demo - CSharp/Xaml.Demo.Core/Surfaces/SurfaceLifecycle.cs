using System;
using Xaml_Demo.Logging;

namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Standard lifecycle states for a render surface. Surfaces progress monotonically
    /// (Constructed -> Initializing -> Initialized -> Disposed).
    /// </summary>
    public enum SurfaceLifecycleState
    {
        Constructed = 0,
        Initializing,
        Initialized,
        Disposed
    }

    /// <summary>
    /// Helper utilities for lifecycle transition logging.
    /// </summary>
    public static class SurfaceLifecycle
    {
        public static void LogTransition(FrameworkSurfaceKind kind, SurfaceLifecycleState from, SurfaceLifecycleState to, string? detail = null)
        {
            LogRouter.Write($"LIFECYCLE[{kind}]: {from} -> {to}{(detail is null ? string.Empty : $" ({detail})")}");
        }

        public static void LogEvent(FrameworkSurfaceKind kind, string message)
        {
            LogRouter.Write($"LIFECYCLE[{kind}]: {message}");
        }
    }
}
