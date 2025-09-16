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

    public ExistingMauiViewSurface(FrameworkSurfaceKind kind, View view)
    {
        Kind = kind;
        _view = view;
    }

    public FrameworkSurfaceKind Kind { get; }

    public View? MauiViewHost => _view;

    public Task InitializeAsync(object context, CancellationToken ct)
    {
        // Nothing to do for an existing view host in Stage 1.
        return Task.CompletedTask;
    }
}
