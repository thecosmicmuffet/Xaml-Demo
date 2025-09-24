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
using Xaml_Demo.Controls;
using Xaml_Demo.Perf;
using System.Runtime.CompilerServices;

namespace Xaml_Demo.Views.Scenarios;

public partial class MultiVisualPerfView : ContentView
{
    private readonly List<IRenderSurface> _surfaces = new();
    private int _mauiRealizationCount;
    private bool _mauiPerfSubscribed;
    private CollectionView? _mauiCollectionView; // dynamic left surface instance
    private int _mauiSessionId;

    public MultiVisualPerfView()
    {
        InitializeComponent();
        LogHub.Write("MultiVisualPerfView created");
        if (BindingContext is not MultiVisualPerfViewModel vm)
            return;
        vm.CurrentColorState = "Normal";
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (BindingContext is MultiVisualPerfViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            await EnsureSurfacesAsync(vm);
        }
    }

    private Task EnsureSurfacesAsync(MultiVisualPerfViewModel vm)
    {
        if (_surfaces.Count > 0) return Task.CompletedTask;

        foreach (var kind in vm.OrderedSurfaceKinds)
        {
            switch (kind)
            {
                case FrameworkSurfaceKind.MauiCollection:
                    if (LeftSurfaceHost != null)
                    {
                        var surf = new MauiCollectionSurface(vm);
                        // Initialize (synchronous currently)
                        surf.InitializeAsync(vm, CancellationToken.None).GetAwaiter().GetResult();
                        var host = surf.MauiViewHost;
                        if (host != null)
                        {
                            _mauiCollectionView = host as CollectionView;
                            LeftSurfaceHost.Content = host;
                            _surfaces.Add(surf);
                            _mauiSessionId = SurfacePerfAggregator.Start(FrameworkSurfaceKind.MauiCollection, PerfConfig.FirstRealizationSampleCount);
                            SubscribeMauiPerf();
                        }
                    }
                    break;
                case FrameworkSurfaceKind.WinUIListView:
                    if (RightSurfaceHost != null)
                    {
                        var surf = new WinUIListViewSurface(vm);
                        surf.InitializeAsync(vm, CancellationToken.None).GetAwaiter().GetResult();
                        var host = surf.MauiViewHost;
                        if (host != null)
                        {
                            RightSurfaceHost.Content = host;
                            _surfaces.Add(surf);
                        }
                    }
                    break;
                case FrameworkSurfaceKind.UwpPlaceholder:
                    // External / simulated or placeholder surface not hostable in-process (no MAUI view).
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private async void OnSwapColors(object? sender, EventArgs e)
    {
        LogHub.Write("SwapColors: start");
        var perfTimer = new StopwatchPerfTimer();
        perfTimer.Start("SwapColors");

        if (BindingContext is not MultiVisualPerfViewModel vm)
        {
            LogHub.Write("SwapColors: no view model");
            return;
        }

        LogHub.Write($"SwapColors: expected {vm.Items.Count} color changes");

        bool completed = await vm.AwaitColorChangesAsync(TimeSpan.FromSeconds(2));

        perfTimer.Stop();
        LogHub.Write(completed
            ? "SwapColors: all updates observed"
            : "SwapColors: timeout waiting for updates");
    }

    private async void OnChangeVisualState(object? sender, EventArgs e)
    {
        if (VisualStateManager.GetVisualStateGroups(this) is not IList<VisualStateGroup> groups || groups.Count == 0)
            return;
        var perfTimer = new StopwatchPerfTimer();
        perfTimer.Start("ChangeVisualState");

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

        // Dynamically assign ItemTemplate based on target visual state (since XAML setters were removed).
        ApplyTemplateForState(newState);

        // Expected number of item realizations to consider viewport "ready"
        int expected = EstimateViewportTarget();
        LogHub.Write($"VS: switching to {newState}; waiting for {expected} first binds");

        // Subscribe BEFORE changing state so we catch earliest realizations
        var waitTask = AwaitFirstBindBatchAsync(expected, TimeSpan.FromSeconds(2));

        VisualStateManager.GoToState(this, newState);

        bool completed = await waitTask.ConfigureAwait(false);

        perfTimer.Stop();
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
        var perfTimer = new StopwatchPerfTimer();
        perfTimer.Start("ToggleSelectAll");
        vm.ToggleSelectAll();
        ForceSelectorRefreshIfNeeded();
        perfTimer.Stop();
        LogHub.Write($"ToggleSelectAll: Selected={vm.SelectedCount}");
    }

    // Alternate selection mechanism: drive selection purely by the item view model's
    // Selected property (used by the SelectableByProperty visual state).
    private async void OnToggleSelectAllViaPropertyAsync(object? sender, EventArgs e)
    {
        if (BindingContext is not MultiVisualPerfViewModel vm)
            return;

        LogHub.Write("ToggleSelectAllViaProperty: start");
        var perfTimer = new StopwatchPerfTimer();
        perfTimer.Start("ToggleSelectAllViaProperty");

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

        perfTimer.Stop();
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
        if (_mauiCollectionView == null) return;
        if (VisualStateManager.GetVisualStateGroups(this) is not IList<VisualStateGroup> groups || groups.Count == 0)
            return;
        string state = groups[0].CurrentState?.Name ?? "Normal";
        if (state == "Selectable")
        {
            var current = _mauiCollectionView.ItemTemplate;
            _mauiCollectionView.ItemTemplate = null;
            _mauiCollectionView.ItemTemplate = current;
        }
    }

    private void ApplyTemplateForState(string state)
    {
        if (_mauiCollectionView == null) return;
        if (Resources == null) return;

        DataTemplate? Resolve(string key)
            => Resources.TryGetValue(key, out var obj) ? obj as DataTemplate : null;

        DataTemplate? template = state switch
        {
            "Normal" => Resolve("PerfItemTemplate"),
            "Highlighted" => Resolve("PerfItemTemplateSelected"),
            "Selectable" => Resolve("PerfItemTemplateSelector"),
            "SelectableByProperty" => Resolve("PerfItemTemplateByProp"),
            _ => Resolve("PerfItemTemplate")
        };

        if (template != null && _mauiCollectionView.ItemTemplate != template)
        {
            _mauiCollectionView.ItemTemplate = template;
        }
    }

    private int EstimateViewportTarget()
    {
        double h = _mauiCollectionView?.Height ?? double.NaN;
        if (double.IsNaN(h) || h <= 0)
            return 30;
        int estimate = (int)Math.Ceiling(h / 24.0) + 5;
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

    private void SubscribeMauiPerf()
    {
        if (_mauiPerfSubscribed) return;
        FirstBindTracker.FirstBind += OnFirstBindMaui;
        _mauiPerfSubscribed = true;
    }

    private void OnFirstBindMaui(VisualElement ve, object? ctx)
    {
        if (ctx == null) return;
        SurfacePerfAggregator.RecordRealized(FrameworkSurfaceKind.MauiCollection, RuntimeHelpers.GetHashCode(ve), _mauiSessionId, "FirstBindTracker");
        _mauiRealizationCount++;
        if (_mauiRealizationCount >= PerfConfig.FirstRealizationSampleCount)
        {
            FirstBindTracker.FirstBind -= OnFirstBindMaui;
        }
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
    /*
    // Pan gesture over transparent overlay to perform lasso selection.
    private void OnLassoPan(object? sender, PanUpdatedEventArgs e)
    {
        if (BindingContext is not MultiVisualPerfViewModel vm) return;
        if (!IsInSelectableState()) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _lassoStart = new Point(e.TotalX, e.TotalY);
                ShowLasso(0, 0, 0, 0);
                break;

            case GestureStatus.Running:
                if (_lassoStart is Point start)
                {
                    double curX = start.X + e.TotalX;
                    double curY = start.Y + e.TotalY;
                    double x = Math.Min(start.X, curX);
                    double y = Math.Min(start.Y, curY);
                    double w = Math.Abs(curX - start.X);
                    double h = Math.Abs(curY - start.Y);
                    ShowLasso(x, y, w, h);
                }
                break;

            case GestureStatus.Canceled:
            case GestureStatus.Completed:
                if (_lassoStart is Point s && LassoRect != null && LassoRect.IsVisible)
                {
                    var rect = CurrentLassoRect();
                    ApplyLassoSelection(vm, rect);
                }
                HideLasso();
                _lassoStart = null;
                break;
        }
    }
     
    private void ShowLasso(double x, double y, double w, double h)
        {
            if (LassoRect == null) return;
            if (!LassoRect.IsVisible) LassoRect.IsVisible = true;

            // Position via Translation to avoid affecting layout.
            LassoRect.TranslationX = x;
            LassoRect.TranslationY = y;
            LassoRect.WidthRequest = w;
            LassoRect.HeightRequest = h;
        }

        private Rect CurrentLassoRect()
        {
            if (LassoRect == null || !LassoRect.IsVisible)
                return Rect.Zero;
            return new Rect(LassoRect.TranslationX, LassoRect.TranslationY, LassoRect.Width, LassoRect.Height);
        }

        private void HideLasso()
        {
            if (LassoRect != null)
            {
                LassoRect.IsVisible = false;
                LassoRect.WidthRequest = -1;
                LassoRect.HeightRequest = -1;
            }
        } 

        private void ApplyLassoSelection(MultiVisualPerfViewModel vm, Rect lasso)
        {
            if (lasso.Width <= 2 || lasso.Height <= 2)
            {
                // Treat tiny drags as clicks; nothing extra here.
                return;
            }

            if (ItemsCollectionView == null) return;

            // Attempt to get realized item views. In MAUI, CollectionView exposes VisibleViews.
            var visibleViewsProp = ItemsCollectionView.GetType().GetProperty("VisibleViews");
            var visible = visibleViewsProp?.GetValue(ItemsCollectionView) as IEnumerable<View>;
            if (visible == null)
            {
                LogHub.Write("Lasso: no VisibleViews; skipping.");
                return;
            }

            var inside = new List<PerfItemViewModel>();

            foreach (var view in visible)
            {
                if (view?.BindingContext is not PerfItemViewModel item) continue;

                // Approximate bounds: use view.Bounds (relative to internal layout). Assume internal layout origin aligned.
                var b = view.Bounds;

                // Inflate a little if zero-sized during layout transitions
                if (b.Width <= 0 || b.Height <= 0)
                    continue;

                if (RectsIntersect(lasso, b))
                    inside.Add(item);
            }

            if (inside.Count == 0)
            {
                LogHub.Write("Lasso: no items inside.");
                return;
            }

            // Decide add or remove: if every item is already selected, deselect; else add missing ones.
            int already = inside.Count(i => vm.SelectedItems.Contains(i));
            if (already == inside.Count)
            {
                vm.ApplySelection(inside, false);
                LogHub.Write($"Lasso: removed {inside.Count} items; Selected={vm.SelectedCount}");
            }
            else
            {
                var toAdd = inside.Where(i => !vm.SelectedItems.Contains(i)).ToList();
                if (toAdd.Count > 0)
                {
                    vm.ApplySelection(toAdd, true);
                    LogHub.Write($"Lasso: added {toAdd.Count} items; Selected={vm.SelectedCount}");
                }
            }

            ForceSelectorRefreshIfNeeded();
        }

        private static bool RectsIntersect(Rect a, Rect b)
            => a.Right >= b.Left && a.Left <= b.Right && a.Bottom >= b.Top && a.Top <= b.Bottom;
    */
}
