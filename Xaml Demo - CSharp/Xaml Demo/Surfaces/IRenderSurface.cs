using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Xaml_Demo.Surfaces;

/// <summary>
/// Abstraction for a visual surface orchestrated by a host view.
/// Stage 1: in-process MAUI-hostable views.
/// Stage 3+: may expose native embedding handles (HWND, island handle) and raise invalidation events
/// to notify hosts of visual/data changes without tight coupling.
/// </summary>
public interface IRenderSurface
{
    /// <summary>
    /// Kind / classifier for diagnostics and ordering.
    /// </summary>
    FrameworkSurfaceKind Kind { get; }

    /// <summary>
    /// Current lifecycle state (monotonic progression).
    /// </summary>
    SurfaceLifecycleState State { get; }

    /// <summary>
    /// If the surface can be directly hosted inside a MAUI visual tree,
    /// this returns the root MAUI View. Null if the surface is non-MAUI
    /// (e.g. future out-of-process / external composition surface).
    /// </summary>
    View? MauiViewHost { get; }

    /// <summary>
    /// Initializes (and if applicable creates) the underlying surface.
    /// Idempotent; multiple calls should be safe.
    /// </summary>
    Task InitializeAsync(object context, CancellationToken ct);

    /// <summary>
    /// Returns a native handle for embedding (HWND, island, etc.) when applicable.
    /// Null when the surface is purely in-process MAUI-only or not yet created.
    /// Host should treat the handle as non-owned unless the returned NativeHandleRef.Owned = true.
    /// </summary>
    Task<NativeHandleRef?> GetEmbedHandleAsync(CancellationToken ct);

    /// <summary>
    /// Raised when the surface needs the host to reconsider layout / visuals / data.
    /// </summary>
    event EventHandler<SurfaceInvalidatedEventArgs>? Invalidated;
}
