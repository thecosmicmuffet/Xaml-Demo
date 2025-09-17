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
        LoggerText.SizeChanged += OnLoggerTextChanged;
    }

    private void OnLoggerTextChanged(object sender, EventArgs e)
    {
        // if the scrollviewer was within 10 pixels of the bottom, scroll to bottom
        if (LoggerScrollViewer.ScrollY + LoggerScrollViewer.Height + 10 <= LoggerScrollViewer.ContentSize.Height)
        {
            LoggerScrollViewer.ScrollToAsync(0, LoggerScrollViewer.ContentSize.Height, true);
        }
    }
}
