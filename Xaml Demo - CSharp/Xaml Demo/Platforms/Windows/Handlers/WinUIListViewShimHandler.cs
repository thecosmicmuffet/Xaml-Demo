#if WINDOWS
using Microsoft.Maui.Handlers;
using WinUIListView = Microsoft.UI.Xaml.Controls.ListView;
using WinUISelectionMode = Microsoft.UI.Xaml.Controls.ListViewSelectionMode;
using WinUIListViewBase = Microsoft.UI.Xaml.Controls.ListViewBase;
using WinUIContainerContentChangingEventArgs = Microsoft.UI.Xaml.Controls.ContainerContentChangingEventArgs;
using Xaml_Demo.Perf;
using Xaml_Demo.Surfaces;

namespace Xaml_Demo.Controls;

/// <summary>
/// Windows platform handler that maps the WinUIListViewShim to a native WinUI ListView.
/// This enables experimentation with alternate rendering paths while sharing MAUI layout.
/// </summary>
public class WinUIListViewShimHandler : ViewHandler<WinUIListViewShim, WinUIListView>
{
    public static readonly IPropertyMapper<WinUIListViewShim, WinUIListViewShimHandler> Mapper =
        new PropertyMapper<WinUIListViewShim, WinUIListViewShimHandler>(ViewHandler.ViewMapper)
        {
            [nameof(WinUIListViewShim.ItemsSource)] = MapItemsSource
        };

    public WinUIListViewShimHandler() : base(Mapper)
    {
    }

    protected override WinUIListView CreatePlatformView() => new WinUIListView
    {
        SelectionMode = WinUISelectionMode.None
    };

    private int _realizationCount;

    protected override void ConnectHandler(WinUIListView platformView)
    {
        base.ConnectHandler(platformView);

        // Start perf aggregation for WinUI surface
        SurfacePerfAggregator.Start(FrameworkSurfaceKind.WinUIListView, PerfConfig.FirstRealizationSampleCount);

        platformView.ContainerContentChanging += OnContainerContentChanging;

        // Initial sync
        MapItemsSource(this, VirtualView);
    }

    protected override void DisconnectHandler(WinUIListView platformView)
    {
        platformView.ContainerContentChanging -= OnContainerContentChanging;
        base.DisconnectHandler(platformView);
        // Ensure summary if not yet emitted
        SurfacePerfAggregator.Flush(FrameworkSurfaceKind.WinUIListView);
    }

    private void OnContainerContentChanging(WinUIListViewBase sender, WinUIContainerContentChangingEventArgs args)
    {
        if (args == null) return;
        if (args.InRecycleQueue) return;
        if (args.Item == null) return;

        SurfacePerfAggregator.RecordRealized(FrameworkSurfaceKind.WinUIListView);
        _realizationCount++;
        if (_realizationCount >= PerfConfig.FirstRealizationSampleCount)
        {
            sender.ContainerContentChanging -= OnContainerContentChanging;
        }
    }

    public static void MapItemsSource(WinUIListViewShimHandler handler, WinUIListViewShim view)
    {
        if (handler.PlatformView is WinUIListView listView)
        {
            listView.ItemsSource = view.ItemsSource;
        }
    }

    public void UpdateItemsSource() => MapItemsSource(this, VirtualView);
}
#endif
