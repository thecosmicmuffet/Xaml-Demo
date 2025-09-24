using System;
using System.Collections.Generic;

namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Provides ordered surface kinds (and later concrete surface factories) for a host view.
    /// Stage 3: only ordering abstraction; later iterations may expose factory methods returning IRenderSurface instances.
    /// </summary>
    public interface ISurfaceCatalog
    {
        IReadOnlyList<FrameworkSurfaceKind> OrderedKinds { get; }
    }

    /// <summary>
    /// Default immutable catalog used in early stages. Singleton to avoid allocations and enable simple DI registration.
    /// </summary>
    public sealed class DefaultSurfaceCatalog : ISurfaceCatalog
    {
        public static DefaultSurfaceCatalog Instance { get; } = new DefaultSurfaceCatalog();

        private static readonly FrameworkSurfaceKind[] _ordered =
        {
            FrameworkSurfaceKind.MauiCollection,
            FrameworkSurfaceKind.WinUIListView,
            FrameworkSurfaceKind.UwpPlaceholder // Added for ExternalProcessSurface (stub / simulated external)
        };

        private DefaultSurfaceCatalog() { }

        public IReadOnlyList<FrameworkSurfaceKind> OrderedKinds => _ordered;
    }
}
