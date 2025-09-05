using Microsoft.Maui.Controls;
using Xaml_Demo.Services;

namespace Xaml_Demo.ViewModels;

public class MainViewModel : BaseViewModel
{
    public LogHub Logger => LogHub.Instance;

    private View? _demoView;
    public View? DemoView
    {
        get => _demoView;
        set => SetProperty(ref _demoView, value);
    }

    public MainViewModel()
    {
        Services.NavigationHub.NavigateRequested += key =>
        {
            var view = Services.ScenarioCatalog.Resolve(key);
            if (view != null) DemoView = view;
            else Log($"Unknown scenario: {key}");
        };

        var initial = Services.ScenarioCatalog.Resolve("MultiVisualPerf");
        if (initial != null) DemoView = initial;
    }

}
