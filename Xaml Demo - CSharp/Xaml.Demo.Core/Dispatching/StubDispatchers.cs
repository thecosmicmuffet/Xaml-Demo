using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xaml_Demo.Dispatching
{
    /// <summary>
    /// Placeholder WPF dispatcher adapter. Real implementation will live in the WPF host
    /// project (which can reference System.Windows and its Dispatcher). This stub exists
    /// in Core solely to satisfy early composition / dependency registrations and to
    /// document intended behavior.
    ///
    /// When implemented in host:
    ///  - Wrap System.Windows.Threading.Dispatcher (Application.Current.Dispatcher)
    ///  - IsDispatchRequired => dispatcher.CheckAccess() == false
    ///  - Post => dispatcher.BeginInvoke(...)
    ///  - SwitchAsync => dispatcher.InvokeAsync(...) awaited
    /// </summary>
    public sealed class WpfDispatcherAdapter : IUiDispatcher
    {
        public bool IsDispatchRequired => false; // Stub: no marshal needed (will be replaced).

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            // Stub executes inline. Real impl will marshal.
            action();
        }

        public Task SwitchAsync(CancellationToken cancellationToken = default)
        {
            // Stub: already considered on dispatcher.
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Placeholder UWP / WinUI2 Island dispatcher adapter. Real implementation will
    /// require a CoreDispatcher (Windows.UI.Core) or DispatcherQueue for WinUI3.
    ///
    /// When implemented in host:
    ///  - Capture CoreDispatcher via island root element.Dispatcher
    ///  - IsDispatchRequired => !dispatcher.HasThreadAccess
    ///  - Post => dispatcher.RunAsync(CoreDispatcherPriority.Normal, ...)
    ///  - SwitchAsync => same but await completion
    /// </summary>
    public sealed class UwpDispatcherAdapter : IUiDispatcher
    {
        public bool IsDispatchRequired => false; // Stub

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            action(); // Stub inline execution
        }

        public Task SwitchAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
