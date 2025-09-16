using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Xaml_Demo.Surfaces;

/// <summary>
/// Abstraction for a visual surface that can be orchestrated by a host view.
/// Stage 1 focuses on in-process MAUI-hostable views. Later stages may
/// extend with native handles / cross-process concepts.
/// </summary>
public interface IRenderSurface
{
    /// <summary>
    /// Kind / classifier for diagnostics and ordering.
    /// </summary>
    FrameworkSurfaceKind Kind { get; }

    /// <summary>
    /// If the surface can be directly hosted inside a MAUI visual tree,
    /// this returns the root MAUI View. Null if the surface is non-MAUI
    /// (e.g. future placeholder for an out-of-process handle).
    /// </summary>
    View? MauiViewHost { get; }

    /// <summary>
    /// Initializes (and if applicable creates) the underlying surface.
    /// Idempotent; multiple calls should be safe.
    /// </summary>
    Task InitializeAsync(object context, CancellationToken ct);
}
