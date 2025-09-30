using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Xaml_Demo.Logging;
using System.Reflection;
using System.IO;
using System.Linq;

namespace Xaml.Demo.Host.Wpf.Hosting
{
    /// <summary>
    /// Creates a WinUI 3 window from WPF context to provide proper activation context for MAUI.
    /// This hybrid approach attempts to bridge the gap between WPF host and MAUI requirements.
    /// </summary>
    public class WinUIWindowHost
    {
        private static readonly SemaphoreSlim _initGate = new(1, 1);
        private static WinUIWindowHost? _instance;
        public static WinUIWindowHost Instance => _instance ??= new WinUIWindowHost();
        
        private object? _winUIApp;
        private object? _winUIWindow;
        private IntPtr _windowHandle = IntPtr.Zero;
        private bool _initialized;
        private Thread? _winUIThread;
        private TaskCompletionSource<bool>? _initTcs;

        private WinUIWindowHost() { }

        public bool IsInitialized => _initialized;
        public IntPtr WindowHandle => _windowHandle;

        /// <summary>
        /// Initialize WinUI 3 window on a separate thread to provide proper activation context
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized) return true;

            await _initGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_initialized) return true;

                LogRouter.Sink?.Write("HOST[WinUIWindow:Start]: Initializing WinUI 3 window host");
                
                // First ensure Windows App Runtime is initialized
                if (!TryInitializeWindowsAppRuntime())
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Error]: Failed to initialize Windows App Runtime");
                    return false;
                }

                _initTcs = new TaskCompletionSource<bool>();

                // Create WinUI on separate STA thread to maintain proper context
                _winUIThread = new Thread(() => RunWinUIThread())
                {
                    Name = "WinUI Host Thread",
                    IsBackground = false
                };
                _winUIThread.SetApartmentState(ApartmentState.STA);
                _winUIThread.Start();

                // Wait for initialization to complete
                var result = await _initTcs.Task.ConfigureAwait(false);
                _initialized = result;
                
                if (_initialized)
                {
                    LogRouter.Sink?.Write($"HOST[WinUIWindow:Initialized]: WindowHandle={_windowHandle:X}");
                }
                
                return _initialized;
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write($"HOST[WinUIWindow:Error]: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            finally
            {
                _initGate.Release();
            }
        }

        private void RunWinUIThread()
        {
            try
            {
                LogRouter.Sink?.Write("HOST[WinUIWindow:Thread]: Starting WinUI thread");
                
                // Load Microsoft.UI.Xaml assembly
                var xamlAssembly = LoadXamlAssembly();
                if (xamlAssembly == null)
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Error]: Could not load Microsoft.UI.Xaml");
                    _initTcs?.SetResult(false);
                    return;
                }

                // Create Application
                var appType = xamlAssembly.GetType("Microsoft.UI.Xaml.Application");
                if (appType == null)
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Error]: Could not find Application type");
                    _initTcs?.SetResult(false);
                    return;
                }

                _winUIApp = Activator.CreateInstance(appType);
                LogRouter.Sink?.Write("HOST[WinUIWindow:Progress]: Created WinUI Application instance");

                // Create Window
                var windowType = xamlAssembly.GetType("Microsoft.UI.Xaml.Window");
                if (windowType == null)
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Error]: Could not find Window type");
                    _initTcs?.SetResult(false);
                    return;
                }

                _winUIWindow = Activator.CreateInstance(windowType);
                LogRouter.Sink?.Write("HOST[WinUIWindow:Progress]: Created WinUI Window instance");

                // Try to get window handle
                if (!TryGetWindowHandle())
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Warning]: Could not get window handle immediately");
                }

                // Set window title
                var titleProp = windowType.GetProperty("Title");
                titleProp?.SetValue(_winUIWindow, "WinUI Host for MAUI");

                // Activate the window (but keep it minimized/hidden initially)
                var activateMethod = windowType.GetMethod("Activate");
                if (activateMethod != null)
                {
                    activateMethod.Invoke(_winUIWindow, null);
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Progress]: Window activated");
                    
                    // Try to get handle again after activation
                    if (_windowHandle == IntPtr.Zero)
                    {
                        Thread.Sleep(100);
                        TryGetWindowHandle();
                    }
                }

                // Hide the window initially
                if (_windowHandle != IntPtr.Zero)
                {
                    ShowWindow(_windowHandle, SW_HIDE);
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Progress]: Window hidden");
                }

                _initTcs?.SetResult(true);

                // Start the message pump
                StartMessagePump();
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write($"HOST[WinUIWindow:ThreadError]: {ex.GetType().Name}: {ex.Message}");
                LogRouter.Sink?.Write($"HOST[WinUIWindow:ThreadStack]: {ex.StackTrace}");
                _initTcs?.SetResult(false);
            }
        }

        private void StartMessagePump()
        {
            try
            {
                LogRouter.Sink?.Write("HOST[WinUIWindow:MessagePump]: Starting WinUI message pump");
                
                // Run the Application (this will block until the app shuts down)
                var runMethod = _winUIApp?.GetType().GetMethod("Run");
                if (runMethod != null && _winUIApp != null)
                {
                    runMethod.Invoke(_winUIApp, null);
                }
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write($"HOST[WinUIWindow:MessagePumpError]: {ex.Message}");
            }
        }

        private bool TryGetWindowHandle()
        {
            try
            {
                if (_winUIWindow == null) return false;

                // Try to get handle via WinRT.Interop.WindowNative
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var winrtAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "WinRT");
                
                if (winrtAssembly != null)
                {
                    var windowNativeType = winrtAssembly.GetType("WinRT.Interop.WindowNative");
                    if (windowNativeType != null)
                    {
                        var getHandleMethod = windowNativeType.GetMethod("GetWindowHandle", 
                            BindingFlags.Public | BindingFlags.Static);
                        
                        if (getHandleMethod != null)
                        {
                            var handle = getHandleMethod.Invoke(null, new[] { _winUIWindow });
                            if (handle is IntPtr hwnd && hwnd != IntPtr.Zero)
                            {
                                _windowHandle = hwnd;
                                LogRouter.Sink?.Write($"HOST[WinUIWindow:Handle]: Got window handle: {hwnd:X}");
                                return true;
                            }
                        }
                    }
                }
                
                LogRouter.Sink?.Write("HOST[WinUIWindow:Handle]: Could not get window handle via WinRT");
                return false;
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write($"HOST[WinUIWindow:HandleError]: {ex.Message}");
                return false;
            }
        }

        private Assembly? LoadXamlAssembly()
        {
            try
            {
                // First check if already loaded
                var loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Microsoft.UI.Xaml");
                
                if (loaded != null)
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Assembly]: Microsoft.UI.Xaml already loaded");
                    return loaded;
                }

                // Try to load from base directory
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var xamlPath = Path.Combine(baseDir, "Microsoft.UI.Xaml.dll");
                
                if (File.Exists(xamlPath))
                {
                    var asm = Assembly.LoadFrom(xamlPath);
                    LogRouter.Sink?.Write($"HOST[WinUIWindow:Assembly]: Loaded Microsoft.UI.Xaml from {xamlPath}");
                    return asm;
                }
                
                // Try Assembly.Load as fallback
                try
                {
                    var asm = Assembly.Load("Microsoft.UI.Xaml");
                    LogRouter.Sink?.Write("HOST[WinUIWindow:Assembly]: Loaded Microsoft.UI.Xaml via Assembly.Load");
                    return asm;
                }
                catch { }

                LogRouter.Sink?.Write("HOST[WinUIWindow:Assembly]: Could not load Microsoft.UI.Xaml");
                return null;
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write($"HOST[WinUIWindow:AssemblyError]: {ex.Message}");
                return null;
            }
        }

        private bool TryInitializeWindowsAppRuntime()
        {
            try
            {
                // Try native initialization with different versions
                uint[] versions = { 0x00010008u, 0x00010007u, 0x00010006u, 0x00010005u };
                
                foreach (var version in versions)
                {
                    try
                    {
                        int hr = MddBootstrapInitialize(version, null, 0);
                        if (hr == 0)
                        {
                            LogRouter.Sink?.Write($"HOST[WinUIWindow:Bootstrap]: Windows App Runtime initialized (version 0x{version:X8})");
                            return true;
                        }
                        else
                        {
                            LogRouter.Sink?.Write($"HOST[WinUIWindow:Bootstrap]: Failed with version 0x{version:X8}, hr=0x{hr:X}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogRouter.Sink?.Write($"HOST[WinUIWindow:Bootstrap]: Exception with version 0x{version:X8}: {ex.Message}");
                    }
                }
                
                LogRouter.Sink?.Write("HOST[WinUIWindow:Bootstrap]: All Windows App Runtime versions failed");
                return false;
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write($"HOST[WinUIWindow:BootstrapError]: {ex.Message}");
                return false;
            }
        }

        public async Task<object?> CreateMauiContentAsync(CancellationToken ct = default)
        {
            if (!_initialized || _winUIWindow == null)
            {
                LogRouter.Sink?.Write("HOST[WinUIWindow:MauiContent]: Window not initialized");
                return null;
            }

            try
            {
                LogRouter.Sink?.Write("HOST[WinUIWindow:MauiContent]: Attempting to create MAUI content in WinUI context");
                
                // Now try to initialize MAUI in the context of the WinUI window
                var bootstrapper = MauiBootstrapper.Instance;
                await bootstrapper.InitializeAsync(ct);
                
                if (bootstrapper.IsInitialized)
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:MauiContent]: MAUI bootstrapper initialized successfully");
                    
                    // Try to get MAUI window handle
                    var mauiHandle = await bootstrapper.EnsureWindowHandleAsync(ct: ct);
                    if (mauiHandle != IntPtr.Zero)
                    {
                        LogRouter.Sink?.Write($"HOST[WinUIWindow:MauiContent]: Got MAUI window handle: {mauiHandle:X}");
                        return mauiHandle;
                    }
                }
                else
                {
                    LogRouter.Sink?.Write("HOST[WinUIWindow:MauiContent]: MAUI bootstrapper failed to initialize");
                }
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write($"HOST[WinUIWindow:MauiContentError]: {ex.GetType().Name}: {ex.Message}");
                LogRouter.Sink?.Write($"HOST[WinUIWindow:MauiContentStack]: {ex.StackTrace}");
            }
            
            return null;
        }

        #region Native Methods
        [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll", CharSet = CharSet.Unicode)]
        private static extern int MddBootstrapInitialize(uint majorMinorVersion, string? versionTag, ulong minVersion);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        #endregion
    }
}
