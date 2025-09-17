using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Dispatching;
using Xaml_Demo.Dispatching;

namespace Xaml_Demo.Dispatching
{
    /// <summary>
    /// MAUI implementation of IUiDispatcher using the platform IDispatcher.
    /// Wraps dispatcher access so Core logic can remain framework-agnostic.
    /// </summary>
    public sealed class MauiDispatcherAdapter : IUiDispatcher
    {
        private readonly Func<IDispatcher?> _dispatcherProvider;

        public MauiDispatcherAdapter()
            : this(() => Application.Current?.Dispatcher)
        {
        }

        public MauiDispatcherAdapter(Func<IDispatcher?> dispatcherProvider)
        {
            _dispatcherProvider = dispatcherProvider ?? throw new ArgumentNullException(nameof(dispatcherProvider));
        }

        private IDispatcher GetDispatcher() =>
            _dispatcherProvider() ?? throw new InvalidOperationException("MAUI dispatcher not yet available.");

        public bool IsDispatchRequired
        {
            get
            {
                var d = _dispatcherProvider();
                // If dispatcher not yet available treat as no marshal required (early startup best-effort).
                if (d == null) return false;
                return d.IsDispatchRequired;
            }
        }

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var d = _dispatcherProvider();
            if (d == null)
            {
                // Best-effort fallback (early startup before Application is fully created).
                action();
                return;
            }

            if (!d.IsDispatchRequired)
            {
                action();
            }
            else
            {
                d.Dispatch(action);
            }
        }

        public Task SwitchAsync(CancellationToken cancellationToken = default)
        {
            if (!IsDispatchRequired)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                var d = GetDispatcher();
                d.Dispatch(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        tcs.TrySetCanceled(cancellationToken);
                    else
                        tcs.TrySetResult(null);
                });
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }
    }

    /// <summary>
    /// Helper to register and initialize the ambient dispatcher for Core.
    /// </summary>
    public static class MauiDispatcherRegistration
    {
        public static void EnsureAmbientInitialized(IServiceProvider services)
        {
            // Try to resolve the adapter; if resolved and ambient not set, set it.
            if (UiDispatcherAmbient.Current == null)
            {
                if (services.GetService(typeof(IUiDispatcher)) is IUiDispatcher adapter)
                {
                    UiDispatcherAmbient.Initialize(adapter);
                }
            }
        }
    }
}
