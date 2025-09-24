using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls; // For consistency with IRenderSurface contract (View reference even though null here)
using Xaml_Demo.Surfaces;
using Xaml_Demo.Services;
using Xaml_Demo.Perf;

namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Simulated external / out-of-process surface (no in-process MAUI visual).
    /// Demonstrates lifecycle transitions + invalidation signaling without a MAUI View.
    /// Uses FrameworkSurfaceKind.UwpPlaceholder until a concrete external host is added.
    /// </summary>
    public sealed class ExternalProcessSurface : IRenderSurface
    {
        private SurfaceLifecycleState _state = SurfaceLifecycleState.Constructed;
        private readonly TimeSpan _initDelay;
        private bool _initialized;

        public ExternalProcessSurface(TimeSpan? simulatedInitDelay = null)
        {
            _initDelay = simulatedInitDelay ?? TimeSpan.FromMilliseconds(150);
        }

        public FrameworkSurfaceKind Kind => FrameworkSurfaceKind.UwpPlaceholder;

        public SurfaceLifecycleState State => _state;

        public View? MauiViewHost => null; // Not hostable in-process

        public event EventHandler<SurfaceInvalidatedEventArgs>? Invalidated;

        public async Task InitializeAsync(object context, CancellationToken ct)
        {
            if (_initialized)
            {
                if (_state != SurfaceLifecycleState.Initialized)
                {
                    var prevRe = _state;
                    _state = SurfaceLifecycleState.Initialized;
                    SurfaceLifecycle.LogTransition(Kind, prevRe, _state, "Re-enter InitializeAsync");
                }
                return;
            }

            var prev = _state;
            _state = SurfaceLifecycleState.Initializing;
            SurfaceLifecycle.LogTransition(Kind, prev, _state, $"Simulated external init ({_initDelay.TotalMilliseconds} ms)");

            try
            {
                await Task.Delay(_initDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                RaiseInvalidated(SurfaceInvalidationKind.Disposed, "Initialization canceled");
                return;
            }

            prev = _state;
            _state = SurfaceLifecycleState.Initialized;
            SurfaceLifecycle.LogTransition(Kind, prev, _state, "Simulated external ready");
            _initialized = true;

            // Notify host that data/visual state may be available.
            RaiseInvalidated(SurfaceInvalidationKind.DataChanged, "SimExternalReady");
        }

        public Task<NativeHandleRef?> GetEmbedHandleAsync(CancellationToken ct)
        {
            // Future: return shared HWND / island / texture handle.
            return Task.FromResult<NativeHandleRef?>(null);
        }

        private void RaiseInvalidated(SurfaceInvalidationKind kind, string? detail = null)
            => Invalidated?.Invoke(this, new SurfaceInvalidatedEventArgs(kind, detail));
    }
}
