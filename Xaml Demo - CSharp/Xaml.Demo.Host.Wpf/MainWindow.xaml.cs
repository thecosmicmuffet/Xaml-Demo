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
            LogRouter.Write("HOST[Lifecycle:HostConstructed]");
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

            // Try Hybrid WinUI 3 Window approach first
            LogRouter.Write("[WPFHost] Attempting Hybrid WinUI 3 Window approach...");
            bool hybridSuccess = await TryHybridWinUIApproach();
            
            if (!hybridSuccess)
            {
                LogRouter.Write("[WPFHost] Hybrid approach failed, falling back to direct MAUI bootstrap");
                // Fallback to direct MAUI initialization (reflection bootstrap + deterministic handle acquisition)
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
                LogRouter.Write("HOST[Lifecycle:Embedded]: hwnd=" + hwnd.ToString("X"));
            }
            catch (Exception ex)
            {
                MauiStatusText.Text = "MAUI embed failed: " + ex.Message;
                LogRouter.Write("[WPFHost] MAUI embed failed: " + ex.Message);
                LogRouter.Write("HOST[Lifecycle:EmbedFail]: reason=" + ex.GetType().Name);
            }
        }

        private async Task<bool> TryHybridWinUIApproach()
        {
            try
            {
                LogRouter.Write("[WPFHost] Initializing WinUI 3 window host...");
                var winUIHost = WinUIWindowHost.Instance;
                
                // Initialize WinUI 3 window first
                bool winUIInitialized = await winUIHost.InitializeAsync();
                if (!winUIInitialized)
                {
                    LogRouter.Write("[WPFHost] Failed to initialize WinUI 3 window");
                    return false;
                }
                
                LogRouter.Write($"[WPFHost] WinUI 3 window initialized, handle: {winUIHost.WindowHandle:X}");
                
                // Now try to create MAUI content within the WinUI context
                LogRouter.Write("[WPFHost] Attempting to create MAUI content in WinUI 3 context...");
                var mauiResult = await winUIHost.CreateMauiContentAsync();
                
                if (mauiResult is IntPtr mauiHwnd && mauiHwnd != IntPtr.Zero)
                {
                    LogRouter.Write($"[WPFHost] Successfully got MAUI window handle from WinUI context: {mauiHwnd:X}");
                    EmbedMaui(mauiHwnd);
                    return true;
                }
                else
                {
                    LogRouter.Write("[WPFHost] Failed to create MAUI content in WinUI 3 context");
                    MauiStatusText.Text = "MAUI initialization failed in WinUI 3 context";
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogRouter.Write($"[WPFHost] Hybrid WinUI approach error: {ex.GetType().Name}: {ex.Message}");
                LogRouter.Write($"[WPFHost] Stack: {ex.StackTrace}");
                return false;
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
