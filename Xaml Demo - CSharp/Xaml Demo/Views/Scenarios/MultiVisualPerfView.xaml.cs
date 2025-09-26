using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Xaml_Demo.Services;
using Xaml_Demo.ViewModels;
using Xaml_Demo.Surfaces;

namespace Xaml_Demo.Views.Scenarios;

public partial class MultiVisualPerfView : ContentView
{
    private readonly List<IRenderSurface> _surfaces = new();

    public MultiVisualPerfView()
    {
        InitializeComponent();
        LogHub.Write("MultiVisualPerfView created");
        if (BindingContext is not MultiVisualPerfViewModel vm)
            return;
        vm.CurrentColorState = "Normal";
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (BindingContext is MultiVisualPerfViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            EnsureSurfaces(vm);
            OnToggleColumns(null, null);
        }
    }

    private void EnsureSurfaces(MultiVisualPerfViewModel vm)
    {
        if (_surfaces.Count > 0) return;

        foreach (var kind in vm.SurfaceOrder)
        {
            switch (kind)
            {
                case FrameworkSurfaceKind.MauiCollection:
                    if (ItemsCollectionView != null)
                        _surfaces.Add(new ExistingMauiViewSurface(FrameworkSurfaceKind.MauiCollection, ItemsCollectionView));
                    else if(RightListView != null)
                        _surfaces.Add(new ExistingMauiViewSurface(FrameworkSurfaceKind.MauiCollection, RightListView));
                    break;
                /* case FrameworkSurfaceKind.WinUIListView:
                    if (RightListShim != null)
                        _surfaces.Add(new ExistingMauiViewSurface(FrameworkSurfaceKind.WinUIListView, RightListShim));
                    break; */
                case FrameworkSurfaceKind.UwpPlaceholder:
                    // Placeholder for future UWP / out-of-process surface.
                    break;
            }
        }
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

        string current = groups[0].CurrentState?.Name ?? "Normal";
        string newState = current switch
        {
            "Normal" => "Highlighted",
            "Highlighted" => "Selectable",
            "Selectable" => "SelectableByProperty",
            _ => "Normal"
        };

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

        if (BindingContext is not MultiVisualPerfViewModel vm)
            return;
        vm.CurrentColorState = newState;
    }

    private void OnToggleSelectAll(object? sender, EventArgs e)
    {
        if (BindingContext is not MultiVisualPerfViewModel vm)
            return;
        LogHub.Write("ToggleSelectAll: start");
        LogHub.StartTimer();
        vm.ToggleSelectAll();
        ForceSelectorRefreshIfNeeded();
        LogHub.StopTimer();
        LogHub.Write($"ToggleSelectAll: Selected={vm.SelectedCount}");
    }

    // Alternate selection mechanism: drive selection purely by the item view model's
    // Selected property (used by the SelectableByProperty visual state).
    private async void OnToggleSelectAllViaPropertyAsync(object? sender, EventArgs e)
    {
        if (BindingContext is not MultiVisualPerfViewModel vm)
            return;

        LogHub.Write("ToggleSelectAllViaProperty: start");
        LogHub.StartTimer();

        bool allSelected = vm.Items.Count > 0 && vm.Items.All(i => i.Selected);
        bool target = !allSelected;

        var tcs = new TaskCompletionSource<bool>();
        int seen = 0;

        void Handler(object? s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PerfItemViewModel.Color))
            {
                if (Interlocked.Increment(ref seen) == vm.Items.Count)
                {
                    tcs.TrySetResult(true);
                }
            }
        }

        foreach (var item in vm.Items)
            item.PropertyChanged += Handler;


        foreach (var item in vm.Items)
        {
            item.Selected = target;
        }

        LogHub.StopTimer();
        int count = vm.Items.Count(i => i.Selected);
        LogHub.Write($"ToggleSelectAllViaProperty: Selected={count}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using (cts.Token.Register(() => tcs.TrySetResult(false)))
        {
            try
            {
                var result = await tcs.Task.ConfigureAwait(false);
                if (!result)
                {
                    LogHub.Write("ToggleSelectAllViaProperty: timeout waiting for color updates");
                }
            }
            finally
            {
                foreach (var item in vm.Items)
                    item.PropertyChanged -= Handler;
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MultiVisualPerfViewModel.SelectionVersion))
        {
            // Force refresh when selection membership changes while in Selectable state
            ForceSelectorRefreshIfNeeded();
        }
    }

    private void ForceSelectorRefreshIfNeeded()
    {
        if (VisualStateManager.GetVisualStateGroups(this) is not IList<VisualStateGroup> groups || groups.Count == 0)
            return;
        string state = groups[0].CurrentState?.Name ?? "Normal";
        if (state == "Selectable")
        {
            // Re-assign template to force DataTemplateSelector reevaluation
            var current = ItemsCollectionView.ItemTemplate;
            ItemsCollectionView.ItemTemplate = null;
            ItemsCollectionView.ItemTemplate = current;
        }
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

    // Click (tap) handler wired via DataTemplate gesture recognizers.
    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is not MultiVisualPerfViewModel vm) return;
        if (!IsInSelectableState()) return;

        var item = e.Parameter as PerfItemViewModel;
        if (item == null) return;

        bool isSelected = vm.SelectedItems.Contains(item);
        vm.ApplySelection(new[] { item }, !isSelected);
        // Force template re-eval if needed
        ForceSelectorRefreshIfNeeded();
        LogHub.Write($"TapSelect: {(isSelected ? "Removed" : "Added")} {item.ColorString}; Count={vm.SelectedCount}");
    }

    // Lasso (drag) selection state
    Point? _lassoStart;

    private bool IsInSelectableState()
    {
        if (VisualStateManager.GetVisualStateGroups(this) is not IList<VisualStateGroup> groups || groups.Count == 0)
            return false;

        var currentState = groups[0].CurrentState?.Name ?? "Normal";
        return currentState == "Selectable" || currentState == "SelectableByProperty";
    }

    private void OnToggleColumns(object? sender, EventArgs e)
    {
        if (RightColumn == null || LeftColumn == null)
            return;
        bool isRightVisible = RightColumn.IsVisible;
        VisualStateManager.GoToState(this, isRightVisible ? "LeftOnly" : "Both");
    }
}
