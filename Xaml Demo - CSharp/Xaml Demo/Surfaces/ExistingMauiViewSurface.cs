using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Xaml_Demo.Surfaces;

/// <summary>
/// Simple wrapper that turns an already-instantiated MAUI View (present in XAML)
/// into an IRenderSurface. This lets us introduce the abstraction without
/// restructuring existing XAML immediately.
/// </summary>
public sealed class ExistingMauiViewSurface : IRenderSurface
{
    private readonly View _view;
    private SurfaceLifecycleState _state = SurfaceLifecycleState.Constructed;

    public ExistingMauiViewSurface(FrameworkSurfaceKind kind, View view)
    {
        Kind = kind;
        _view = view;
    }

    public FrameworkSurfaceKind Kind { get; }
    public SurfaceLifecycleState State => _state;

    public View? MauiViewHost => _view;

    public event EventHandler<SurfaceInvalidatedEventArgs>? Invalidated;

    public Task InitializeAsync(object context, CancellationToken ct)
    {
        if (_state == SurfaceLifecycleState.Initialized)
            return Task.CompletedTask;

        var prev = _state;
        _state = SurfaceLifecycleState.Initializing;
        SurfaceLifecycle.LogTransition(Kind, prev, _state, "Existing view host");

        // Existing view already created; no async work yet.
        prev = _state;
        _state = SurfaceLifecycleState.Initialized;
        SurfaceLifecycle.LogTransition(Kind, prev, _state);

        return Task.CompletedTask;
    }

    public Task<NativeHandleRef?> GetEmbedHandleAsync(CancellationToken ct)
    {
        // Existing MAUI view surface does not expose a separate native embedding handle.
        return Task.FromResult<NativeHandleRef?>(null);
    }
}
