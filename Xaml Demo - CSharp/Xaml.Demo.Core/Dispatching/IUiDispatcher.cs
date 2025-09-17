using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xaml_Demo.Dispatching
{
    /// <summary>
    /// Platform-agnostic UI thread dispatcher abstraction.
    /// Provides minimal operations needed for cross-surface coordination
    /// without binding Core logic to specific UI frameworks.
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>
        /// True if the caller is NOT on the dispatcher thread and must marshal.
        /// </summary>
        bool IsDispatchRequired { get; }

        /// <summary>
        /// Queue an action to run asynchronously on the UI thread.
        /// Fire-and-forget semantics.
        /// </summary>
        void Post(Action action);

        /// <summary>
        /// Switches to (or confirms) execution on the UI thread.
        /// Equivalent intent to awaiting a framework-specific dispatcher invoke.
        /// </summary>
        Task SwitchAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Simple ambient accessor for scenarios where explicit injection is awkward.
    /// Host layers should set this early in startup (e.g., MauiProgram or WPF App.OnStartup).
    /// Core logic can fallback gracefully if not set.
    /// </summary>
    public static class UiDispatcherAmbient
    {
        private static IUiDispatcher? _current;
        public static IUiDispatcher? Current => _current;

        public static void Initialize(IUiDispatcher dispatcher)
        {
            _current = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public static void Post(Action action)
        {
            if (_current == null)
            {
                // No dispatcher yet; run inline (best effort).
                action();
                return;
            }
            _current.Post(action);
        }

        public static Task SwitchAsync(CancellationToken ct = default)
        {
            if (_current == null)
            {
                return Task.CompletedTask;
            }
            if (!_current.IsDispatchRequired)
            {
                return Task.CompletedTask;
            }
            return _current.SwitchAsync(ct);
        }
    }
}
