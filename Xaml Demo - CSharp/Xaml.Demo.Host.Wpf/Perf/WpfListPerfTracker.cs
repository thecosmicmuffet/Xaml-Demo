using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows;
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

        public WpfListPerfTracker(ListBox list, FrameworkSurfaceKind kind)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _kind = kind;
            _target = PerfConfig.FirstRealizationSampleCount;

            _list.ItemContainerGenerator.StatusChanged += OnStatusChanged;
            _list.LayoutUpdated += OnLayoutUpdated;
            _list.Unloaded += OnUnloaded;
        }

        private void EnsureStarted()
        {
            if (_started) return;
            SurfacePerfAggregator.Start(_kind, _target);
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
                        _count++;
                        var elapsed = _sw.Elapsed.TotalMilliseconds;
                        // Record with container identity (id) – include session for stale filtering.
                        SurfacePerfAggregator.RecordRealized(_kind);
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
    }
}
