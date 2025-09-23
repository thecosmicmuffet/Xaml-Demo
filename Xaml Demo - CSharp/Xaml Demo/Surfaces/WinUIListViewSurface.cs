using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Xaml_Demo.ViewModels;

namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Surface wrapper that creates and hosts a WinUIListViewShim (MAUI control shim) for the
    /// MultiVisualPerfViewModel item collection. Stage 3: created dynamically instead of being
    /// declared in XAML to exercise the surface abstraction mounting path.
    /// </summary>
    public sealed class WinUIListViewSurface : IRenderSurface
    {
        private readonly MultiVisualPerfViewModel _vm;
        private View? _host;
        private SurfaceLifecycleState _state = SurfaceLifecycleState.Constructed;

        public WinUIListViewSurface(MultiVisualPerfViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        }

        public FrameworkSurfaceKind Kind => FrameworkSurfaceKind.WinUIListView;

        public View? MauiViewHost => _host;

        public SurfaceLifecycleState State => _state;

        public event EventHandler<SurfaceInvalidatedEventArgs>? Invalidated;

        public Task InitializeAsync(object context, CancellationToken ct)
        {
            if (_host != null)
            {
                // Already created; ensure state finalized.
                if (_state != SurfaceLifecycleState.Initialized)
                {
                    var prevExisting = _state;
                    _state = SurfaceLifecycleState.Initialized;
                    SurfaceLifecycle.LogTransition(Kind, prevExisting, _state, "Re-enter InitializeAsync");
                }
                return Task.CompletedTask;
            }

            var prev = _state;
            _state = SurfaceLifecycleState.Initializing;
            SurfaceLifecycle.LogTransition(Kind, prev, _state, "Creating WinUIListViewShim");

            // Create shim (handler supplied via MauiProgram)
            var asm = typeof(WinUIListViewSurface).Assembly;
            var shimType = asm.GetType("Xaml_Demo.Controls.WinUIListViewShim");
            if (shimType == null)
            {
                throw new InvalidOperationException("WinUIListViewShim type not found (ensure Controls namespace exists).");
            }

            if (Activator.CreateInstance(shimType) is not View shimView)
            {
                throw new InvalidOperationException("Failed to instantiate WinUIListViewShim.");
            }

            // Set ItemsSource via reflection (avoid hard compile dependency here)
            var itemsSourceProp = shimType.GetProperty("ItemsSource");
            itemsSourceProp?.SetValue(shimView, _vm.Items);

            _host = shimView;

            prev = _state;
            _state = SurfaceLifecycleState.Initialized;
            SurfaceLifecycle.LogTransition(Kind, prev, _state);

            return Task.CompletedTask;
        }

        public Task<NativeHandleRef?> GetEmbedHandleAsync(CancellationToken ct)
            => Task.FromResult<NativeHandleRef?>(null);

        private void RaiseInvalidated(SurfaceInvalidationKind kind, string? detail = null)
            => Invalidated?.Invoke(this, new SurfaceInvalidatedEventArgs(kind, detail));
    }
}
