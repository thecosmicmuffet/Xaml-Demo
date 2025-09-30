using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using Xaml_Demo.Logging;
using Xaml_Demo.Perf;
using Xaml_Demo.Surfaces;

namespace Xaml.Demo.Host.Wpf.Perf
{
    /// <summary>
    /// Tracks first-N ListBoxItem container realizations for the WPF host ListBox and logs
    /// PERF lines via SurfacePerfAggregator (mirrors MAUI / WinUI pathways).
    /// </summary>
    internal sealed class WpfListPerfTracker
    {
        private readonly ListBox _list;
        private readonly FrameworkSurfaceKind _kind;
        private readonly Stopwatch _sw = new();
        private readonly HashSet<int> _seen = new();
        private readonly int _target;
        private int _count;
        private bool _done;
        private bool _started;
        private int _sessionId;

        public WpfListPerfTracker(ListBox list, FrameworkSurfaceKind kind, bool autoRealize = true)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _kind = kind;
            _target = PerfConfig.FirstRealizationSampleCount;

            _list.ItemContainerGenerator.StatusChanged += OnStatusChanged;
            _list.LayoutUpdated += OnLayoutUpdated;
            _list.Unloaded += OnUnloaded;

            if (autoRealize)
            {
                // Kick an async realization loop to drive virtualization to produce first N containers quickly.
                _ = StartAutoRealizeAsync();
            }
        }

        private void EnsureStarted()
        {
            if (_started) return;
            _sessionId = SurfacePerfAggregator.Start(_kind, _target);
            _sw.Start();
            _started = true;
        }

        private void OnStatusChanged(object? sender, EventArgs e) => TrySample();
        private void OnLayoutUpdated(object? sender, EventArgs e) => TrySample();

        private void TrySample()
        {
            if (_done) return;
            EnsureStarted();

            var gen = _list.ItemContainerGenerator;
            var itemsCount = _list.Items.Count;
            bool anyNew = false;

            for (int i = 0; i < itemsCount && !_done; i++)
            {
                var containerObj = gen.ContainerFromIndex(i);
                if (containerObj is ListBoxItem lbi)
                {
                    int id = RuntimeHelpers.GetHashCode(lbi);
                    if (_seen.Add(id))
                    {
                        SurfacePerfAggregator.RecordRealized(_kind, id, _sessionId, "WpfList");
                        _count++;
                        anyNew = true;
                        if (_count >= _target)
                        {
                            _done = true;
                        }
                    }
                }
            }

            if (_done)
            {
                Detach();
                LogRouter.Write($"HOST[Lifecycle:WpfListPerfComplete]: count={_count} target={_target}");
            }
            else if (anyNew)
            {
                // optional progressive log
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (!_done)
            {
                LogRouter.Write("HOST[Lifecycle:WpfListPerfFlush]: reason=Unloaded");
                SurfacePerfAggregator.Flush(_kind);
            }
            Detach();
        }

        private void Detach()
        {
            _list.ItemContainerGenerator.StatusChanged -= OnStatusChanged;
            _list.LayoutUpdated -= OnLayoutUpdated;
            _list.Unloaded -= OnUnloaded;
        }

        private async Task StartAutoRealizeAsync()
        {
            // Allow initial layout to occur
            await Task.Delay(50).ConfigureAwait(true);

            // Use dispatcher to ensure we run on UI thread
            var dispatcher = _list.Dispatcher;
            if (dispatcher == null) return;

            try
            {
                int lastRequested = -1;
                while (!_done && _count < _target)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        // Heuristic: request container near current count to trigger incremental virtualization materialization
                        int nextIndex = Math.Min(_count + 3, _list.Items.Count - 1);
                        if (nextIndex != lastRequested && nextIndex >= 0)
                        {
                            _list.ScrollIntoView(_list.Items[nextIndex]);
                            lastRequested = nextIndex;
                        }
                    }, DispatcherPriority.Background);

                    // Small delay to allow layout pass & generator status change
                    await Task.Delay(25).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogRouter.Write("HOST[Lifecycle:WpfListAutoRealizeError]: " + ex.Message);
            }
        }
    }
}
