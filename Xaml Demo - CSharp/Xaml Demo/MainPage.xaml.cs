using Xaml_Demo.ViewModels;
using Xaml_Demo.Services;

namespace Xaml_Demo;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainViewModel();
        LogHub.Write("MainPage initialized");
    }
}
