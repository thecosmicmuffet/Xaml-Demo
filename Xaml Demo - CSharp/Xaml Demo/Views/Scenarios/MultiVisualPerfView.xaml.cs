using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Xaml_Demo.Services;
using Xaml_Demo.ViewModels;

namespace Xaml_Demo.Views.Scenarios;

public partial class MultiVisualPerfView : ContentView
{
    public MultiVisualPerfView()
    {
        InitializeComponent();
        BindingContext = new MultiVisualPerfViewModel();
        LogHub.Write("MultiVisualPerfView created");
    }

    private async void OnSwapColors(object? sender, EventArgs e)
    {
        LogHub.Write("SwapColors: start");
        LogHub.StartTimer();

        if (BindingContext is not MultiVisualPerfViewModel vm)
        {
            LogHub.Write("SwapColors: no view model");
            return;
        }

        LogHub.Write($"SwapColors: expected {vm.Items.Count} color changes");

        bool completed = await vm.AwaitColorChangesAsync(TimeSpan.FromSeconds(2));

        LogHub.StopTimer();
        LogHub.Write(completed
            ? "SwapColors: all updates observed"
            : "SwapColors: timeout waiting for updates");
    }
    
    private async void OnChangeVisualState(object? sender, EventArgs e)
    {
        if (VisualStateManager.GetVisualStateGroups(this) is not IList<VisualStateGroup> groups || groups.Count == 0)
            return;

        string newState = (groups[0].CurrentState?.Name == "Normal")
            ? "Highlighted"
            : "Normal";

        LogHub.Write("VS: start");
        LogHub.StartTimer();

        // Expected number of item realizations to consider viewport "ready"
        int expected = EstimateViewportTarget();
        LogHub.Write($"VS: switching to {newState}; waiting for {expected} first binds");

        // Subscribe BEFORE changing state so we catch earliest realizations
        var waitTask = AwaitFirstBindBatchAsync(expected, TimeSpan.FromSeconds(2));

        VisualStateManager.GoToState(this, newState);

        bool completed = await waitTask.ConfigureAwait(false);

        LogHub.StopTimer();
        LogHub.Write(completed
            ? $"VS: realization batch complete (>= {expected} items bound)"
            : $"VS: timeout before {expected} items realized");
    }

    private int EstimateViewportTarget()
    {
        // Simple heuristic. If height known, approximate by assuming ~24px per row.
        double h = ItemsCollectionView.Height;
        if (double.IsNaN(h) || h <= 0)
            return 30; // fallback
        int estimate = (int)Math.Ceiling(h / 24.0) + 5; // small buffer
        if (estimate < 15) estimate = 15;
        if (estimate > 100) estimate = 100;
        return estimate;
    }

    private Task<bool> AwaitFirstBindBatchAsync(int expected, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<bool>();
        var distinct = new HashSet<object>();
        void Handler(VisualElement ve, object? ctx)
        {
            if (ctx == null) return;
            if (distinct.Add(ctx) && distinct.Count >= expected)
            {
                tcs.TrySetResult(true);
            }
        }

        FirstBindTracker.FirstBind += Handler;

        var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() =>
        {
            tcs.TrySetResult(false);
        });

        return tcs.Task.ContinueWith(t =>
        {
            FirstBindTracker.FirstBind -= Handler;
            return t.Result;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

/*     private Task NextUiFrameAsync()
            {
                var tcs = new TaskCompletionSource<bool>();
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(16), () => tcs.TrySetResult(true));
                return tcs.Task;
            } */
}
