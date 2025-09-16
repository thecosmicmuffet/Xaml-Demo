#if WINDOWS
using Microsoft.Maui.Handlers;
using WinUIListView = Microsoft.UI.Xaml.Controls.ListView;
using WinUISelectionMode = Microsoft.UI.Xaml.Controls.ListViewSelectionMode;

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

    protected override void ConnectHandler(WinUIListView platformView)
    {
        base.ConnectHandler(platformView);
        // Initial sync
        MapItemsSource(this, VirtualView);
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
