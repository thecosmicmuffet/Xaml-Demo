using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Xaml_Demo.Logging;

namespace Xaml.Demo.Host.Wpf.Hosting
{
    /// <summary>
    /// Reflection-based MAUI bootstrap for the WPF host.
    /// Avoids compile-time dependency on Microsoft.Maui.* packages inside the WPF project.
    /// Future enhancement: replace with direct calls once MAUI hosting path finalized.
    /// </summary>
    public sealed class MauiBootstrapper
    {
        private static readonly SemaphoreSlim _initGate = new(1, 1);
        private static MauiBootstrapper? _instance;
        public static MauiBootstrapper Instance => _instance ??= new MauiBootstrapper();

        private object? _mauiApp;                 // Actual type: MauiApp
        private object? _services;                // IServiceProvider
        private IntPtr _mainWindowHandle = IntPtr.Zero;
        private bool _initialized;
        private bool _forcedWindowAttempted;
        private readonly Stopwatch _lifecycleSw = new();

        private MauiBootstrapper() { }

        public bool IsInitialized => _initialized;
        public IntPtr MainWindowHandle => _mainWindowHandle;
        public object? Services => _services;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized) return;

            await _initGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_initialized) return;

                _lifecycleSw.Restart();
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperStart]: msg=InitializingReflection");

                // Load MAUI app assembly (project reference ensures load).
                var all = AppDomain.CurrentDomain.GetAssemblies();
                var mauiAsm = all.FirstOrDefault(a => a.GetName().Name == "Xaml Demo")
                             ?? Assembly.Load("Xaml Demo");

                var mauiProgramType = mauiAsm.GetType("Xaml_Demo.MauiProgram");
                var createMethod = mauiProgramType?.GetMethod("CreateMauiApp", BindingFlags.Public | BindingFlags.Static);
                if (createMethod == null)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperError]: reason=CreateMauiAppMissing");
                    return;
                }

                LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapping]: phase=CreateMauiApp");
                _mauiApp = createMethod.Invoke(null, null);
                if (_mauiApp == null)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperError]: reason=CreateMauiAppNull");
                    return;
                }

                // Acquire Services property via reflection.
                _services = _mauiApp.GetType().GetProperty("Services")?.GetValue(_mauiApp);
                if (_services == null)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperWarn]: msg=ServicesPropertyMissing");
                }

                // Attempt initial window handle acquisition
                TryAcquireWindowHandle();

                _initialized = true;
                LogRouter.Sink?.Write($"HOST[Lifecycle:MauiBootstrapperInitialized]: elapsed={_lifecycleSw.ElapsedMilliseconds}ms hwnd={( _mainWindowHandle != IntPtr.Zero ? _mainWindowHandle.ToString("X") : "0")}");
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperException]: " + ex.Message);
            }
            finally
            {
                _initGate.Release();
            }
        }

        public Task<IntPtr> GetMainWindowHandleAsync(CancellationToken ct = default)
        {
            if (_mainWindowHandle == IntPtr.Zero)
            {
                TryAcquireWindowHandle();
            }
            return Task.FromResult(_mainWindowHandle);
        }

        /// <summary>
        /// Ensures a non-zero MAUI (WinUI) window handle by optionally forcing window creation.
        /// Retries with backoff; logs lifecycle milestones.
        /// </summary>
        public async Task<IntPtr> EnsureWindowHandleAsync(int retries = 6, int delayMs = 350, CancellationToken ct = default)
        {
            for (int i = 0; i < retries && _mainWindowHandle == IntPtr.Zero; i++)
            {
                ct.ThrowIfCancellationRequested();
                TryAcquireWindowHandle();

                if (_mainWindowHandle == IntPtr.Zero)
                {
                    if (!_forcedWindowAttempted)
                        ForceCreateWindow(); // one-time attempt
                    if (_mainWindowHandle == IntPtr.Zero)
                    {
                        await Task.Delay(delayMs, ct).ConfigureAwait(false);
                    }
                }
            }

            LogRouter.Sink?.Write($"HOST[Lifecycle:MauiHandleResult]: hwnd={( _mainWindowHandle != IntPtr.Zero ? _mainWindowHandle.ToString("X") : "0")}");
            return _mainWindowHandle;
        }

        private void TryAcquireWindowHandle()
        {
#if WINDOWS
            try
            {
                if (_mainWindowHandle != IntPtr.Zero || _services == null) return;

                var getService = _services.GetType().GetMethod("GetService", new[] { typeof(Type) });
                if (getService == null) return;

                var appType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t.FullName == "Microsoft.Maui.Controls.Application");
                if (appType == null) return;

                var appInstance = getService.Invoke(_services, new object[] { appType });
                if (appInstance == null) return;

                var windowsProp = appType.GetProperty("Windows");
                var windowsCollection = windowsProp?.GetValue(appInstance) as System.Collections.IEnumerable;
                var firstWin = windowsCollection?.Cast<object?>().FirstOrDefault();
                if (firstWin == null) return;

                var handlerProp = firstWin.GetType().GetProperty("Handler");
                var handler = handlerProp?.GetValue(firstWin);
                if (handler == null) return;

                var platformViewProp = handler.GetType().GetProperty("PlatformView");
                var platformView = platformViewProp?.GetValue(handler);
                if (platformView == null) return;

                if (platformView.GetType().FullName == "Microsoft.UI.Xaml.Window")
                {
                    var windowNativeType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.DefinedTypes; } catch { return Array.Empty<TypeInfo>(); }
                        })
                        .FirstOrDefault(ti => ti.FullName == "WinRT.Interop.WindowNative");
                    var getHandle = windowNativeType?.GetMethod("GetWindowHandle", BindingFlags.Public | BindingFlags.Static);
                    if (getHandle != null)
                    {
                        var hwndObj = getHandle.Invoke(null, new[] { platformView });
                        if (hwndObj is IntPtr hwnd && hwnd != IntPtr.Zero)
                        {
                            _mainWindowHandle = hwnd;
                            LogRouter.Sink?.Write("HOST[Lifecycle:MauiHandleAcquired]: hwnd=" + hwnd.ToString("X"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiHandleAcquireError]: " + ex.Message);
            }
#endif
        }

        /// <summary>
        /// Attempts to force creation of a MAUI Window if none exists yet (best‑effort).
        /// </summary>
        private void ForceCreateWindow()
        {
            if (_forcedWindowAttempted) return;
            _forcedWindowAttempted = true;

            try
            {
                if (_services == null)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiForceWindowSkip]: reason=NoServices");
                    return;
                }

                var getService = _services.GetType().GetMethod("GetService", new[] { typeof(Type) });
                if (getService == null) return;

                var appType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t.FullName == "Microsoft.Maui.Controls.Application");
                if (appType == null) return;
                var appInstance = getService.Invoke(_services, new object[] { appType });
                if (appInstance == null) return;

                var windowsProp = appType.GetProperty("Windows");
                var windowsCollection = windowsProp?.GetValue(appInstance) as System.Collections.IEnumerable;
                bool any = windowsCollection?.Cast<object?>().Any() == true;
                if (any)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiForceWindowSkip]: reason=ExistingWindow");
                    return;
                }

                // Try assigning a temporary MainPage to trigger window creation
                var mainPageProp = appType.GetProperty("MainPage");
                if (mainPageProp != null && mainPageProp.GetValue(appInstance) == null)
                {
                    var contentPageType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                        })
                        .FirstOrDefault(t => t.FullName == "Microsoft.Maui.Controls.ContentPage");
                    if (contentPageType != null)
                    {
                        var tempPage = Activator.CreateInstance(contentPageType);
                        mainPageProp.SetValue(appInstance, tempPage);
                        LogRouter.Sink?.Write("HOST[Lifecycle:MauiForceWindowAttempt]: action=SetTempMainPage");
                    }
                }

                // Re-attempt acquisition after forced creation path
                TryAcquireWindowHandle();
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiForceWindowError]: " + ex.Message);
            }
        }
    }
}
