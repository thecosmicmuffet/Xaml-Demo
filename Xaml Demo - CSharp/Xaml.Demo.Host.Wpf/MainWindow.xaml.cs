using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xaml_Demo.Logging;
using Xaml_Demo.ViewModels;
using Xaml.Demo.Host.Wpf.Hosting;
using Xaml.Demo.Host.Wpf.Logging;
using Xaml.Demo.Host.Wpf.Perf;
using Xaml_Demo.Surfaces;

namespace Xaml.Demo.Host.Wpf
{
    public partial class MainWindow : Window
    {
        private MultiVisualPerfViewModel? _vm;
        private bool _mauiEmbedded;
        private WpfListPerfTracker? _wpfPerfTracker;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeLogging();

            LogRouter.Write("[WPFHost] MainWindow Loaded: starting bootstrap sequence.");

            // Instantiate Core VM (independent of MAUI availability)
            _vm = new MultiVisualPerfViewModel();
            DataContext = _vm;
            LogRouter.Write("[WPFHost] ViewModel created. Items=" + _vm.Items.Count);

            // Start WPF ListBox perf tracking (first-N realization metrics)
            _wpfPerfTracker = new WpfListPerfTracker(WpfList, FrameworkSurfaceKind.WpfList);

            // Initialize MAUI (reflection bootstrap + deterministic handle acquisition)
            await MauiBootstrapper.Instance.InitializeAsync();
            var hwnd = await MauiBootstrapper.Instance.EnsureWindowHandleAsync();
            if (hwnd != IntPtr.Zero)
            {
                EmbedMaui(hwnd);
            }
            else
            {
                MauiStatusText.Text = "MAUI window handle unavailable (not realized yet).";
                LogRouter.Write("[WPFHost] MAUI handle not acquired; surface placeholder retained.");
            }
        }

        private void InitializeLogging()
        {
            // Combine UI textbox sink + file sink if none already assigned
            if (LogRouter.Sink == null)
            {
                var uiSink = new WpfTextBoxLogSink(LogBox);
                var fileSink = new FileLogSink();
                LogRouter.Sink = new CompositeLogSink(uiSink, fileSink);
                LogRouter.Write("[WPFHost] LogRouter sink initialized (UI + file).");
            }
            else
            {
                LogRouter.Write("[WPFHost] Existing LogRouter sink detected; UI log sink not installed as primary.");
            }
        }

        private void EmbedMaui(IntPtr hwnd)
        {
            if (_mauiEmbedded) return;

            try
            {
                var host = new MauiHwndHost(hwnd);
                MauiHostPlaceholder.Children.Clear();
                MauiHostPlaceholder.Children.Add(host);
                MauiStatusText.Text = "MAUI surface embedded.";
                _mauiEmbedded = true;
                LogRouter.Write("[WPFHost] MAUI window embedded via HwndHost.");
            }
            catch (Exception ex)
            {
                MauiStatusText.Text = "MAUI embed failed: " + ex.Message;
                LogRouter.Write("[WPFHost] MAUI embed failed: " + ex.Message);
            }
        }

        private void OnToggleSelectAll(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.ToggleSelectAll();
            LogRouter.Write("[WPFHost] ToggleSelectAll invoked. Selected=" + _vm.SelectedCount);
        }

        private void OnSwapColors(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var changed = _vm_SWAP();
            LogRouter.Write("[WPFHost] SwapColors invoked. Changed=" + changed);
        }

        // Separate method so we can catch potential exceptions distinctly.
        private int _vm_SWAP()
        {
            try
            {
                return _vm?.SwapColors() ?? 0;
            }
            catch (Exception ex)
            {
                LogRouter.Write("[WPFHost] SwapColors error: " + ex.Message);
                return 0;
            }
        }
    }
}
