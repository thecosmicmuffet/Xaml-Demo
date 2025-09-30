using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Xaml.Demo.Core.Logging;
using Xaml_Demo;

namespace Xaml.Demo.Host.Wpf.Hosting
{
    /// <summary>
    /// Direct MAUI bootstrap for the packaged WPF host.
    /// Uses direct references to MAUI types now that we have package identity.
    /// </summary>
    public sealed class MauiBootstrapper
    {
        private static readonly SemaphoreSlim _initGate = new(1, 1);
        private static MauiBootstrapper? _instance;
        public static MauiBootstrapper Instance => _instance ??= new MauiBootstrapper();

        private MauiApp? _mauiApp;
        private IServiceProvider? _services;
        private IntPtr _mainWindowHandle = IntPtr.Zero;
        private bool _initialized;
        private bool _forcedWindowAttempted;
        private bool _mauiReadyLogged;
        private bool _assemblyResolveHooked;
        private readonly Stopwatch _lifecycleSw = new();

        private MauiBootstrapper() { }

        public bool IsInitialized => _initialized;
        public IntPtr MainWindowHandle => _mainWindowHandle;
        public IServiceProvider? Services => _services;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized) return;

            await _initGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_initialized) return;

                _lifecycleSw.Restart();
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperStart]: msg=InitializingDirect");
                TryWindowsAppRuntimeBootstrap();
                InstallAssemblyResolveHook();
                InstallFirstChanceExceptionHook();

                LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapping]: phase=CreateMauiApp directRef=true");
                try
                {
                    _mauiApp = MauiProgram.CreateMauiApp();
                }
                catch (Exception ex)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiCreateDirectError]: type=" + ex.GetType().FullName + " msg=" + ex.Message);
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiCreateDirectStack]: " + ex);
                    if (ex.InnerException != null)
                        LogRouter.Sink?.Write("HOST[Lifecycle:MauiCreateDirectInner]: inner=" + ex.InnerException.GetType().FullName + " msg=" + ex.InnerException.Message + " stack=" + ex.InnerException.StackTrace);
                    return;
                }

                if (_mauiApp == null)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperError]: reason=CreateMauiAppNull(direct)");
                    return;
                }

                // Get services directly
                _services = _mauiApp.Services;
                if (_services == null)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperWarn]: msg=ServicesPropertyMissing");
                }

                // Attempt initial window handle acquisition
                TryAcquireWindowHandle();

                _initialized = true;
                LogRouter.Sink?.Write($"HOST[Lifecycle:MauiBootstrapperInitialized]: elapsed={_lifecycleSw.ElapsedMilliseconds}ms hwnd={(_mainWindowHandle != IntPtr.Zero ? _mainWindowHandle.ToString("X") : "0")}");
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperException]: " + ex.GetType().FullName + " msg=" + ex.Message);
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperExceptionDetail]: " + ex.ToString());
                if (ex.InnerException != null)
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiBootstrapperInner]: " + ex.InnerException.GetType().FullName + " msg=" + ex.InnerException.Message + " stack=" + ex.InnerException.StackTrace);
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

            if (_mainWindowHandle != IntPtr.Zero && !_mauiReadyLogged)
            {
                _mauiReadyLogged = true;
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiReady]: hwnd=" + _mainWindowHandle.ToString("X"));
            }
            LogRouter.Sink?.Write($"HOST[Lifecycle:MauiHandleResult]: hwnd={(_mainWindowHandle != IntPtr.Zero ? _mainWindowHandle.ToString("X") : "0")}");
            return _mainWindowHandle;
        }

        private void TryAcquireWindowHandle()
        {
#if WINDOWS
            try
            {
                if (_mainWindowHandle != IntPtr.Zero || _services == null) return;

                var app = _services.GetService<Application>();
                if (app == null) return;

                var firstWin = app.Windows.FirstOrDefault();
                if (firstWin == null) return;

                var handler = firstWin.Handler;
                if (handler == null) return;

                var platformView = handler.PlatformView;
                if (platformView == null) return;

                if (platformView is Microsoft.UI.Xaml.Window winUIWindow)
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
                        var hwndObj = getHandle.Invoke(null, new[] { winUIWindow });
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
            if (_forcedWindowAttempted || _services == null) return;
            _forcedWindowAttempted = true;

            try
            {
                var app = _services.GetService<Application>();
                if (app == null) return;

                bool hasWindow = app.Windows.Any();
                if (hasWindow)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiForceWindowSkip]: reason=ExistingWindow");
                    return;
                }

                // Try assigning a temporary MainPage to trigger window creation
                if (app.MainPage == null)
                {
                    app.MainPage = new ContentPage { Title = "MAUI Host" };
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiForceWindowAttempt]: action=SetTempMainPage");
                }

                // Re-attempt acquisition after forced creation path
                TryAcquireWindowHandle();
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiForceWindowError]: " + ex.Message);
            }
        }

        #region Windows App Runtime Bootstrap
        private static bool _bootstrapAttempted;
        private static bool _bootstrapSucceeded;
        private void TryWindowsAppRuntimeBootstrap()
        {
            if (_bootstrapAttempted) return;
            _bootstrapAttempted = true;
            try
            {
                // Attempt managed bootstrapper (two candidate assemblies) first.
                string[] managedCandidates =
                {
                    "Microsoft.WindowsAppRuntime.Bootstrap.Net",
                    "Microsoft.WindowsAppRuntime.Bootstrap"
                };

                foreach (var asmName in managedCandidates)
                {
                    var bootstrapAsm =
                        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == asmName)
                        ?? LoadIfExists(asmName + ".dll");

                    if (bootstrapAsm == null) continue;

                    var bootstrapType = bootstrapAsm.GetType("Microsoft.WindowsAppRuntime.Bootstrapper");
                    if (bootstrapType == null) continue;

                    // Look for Initialize() with zero params first, else any static Initialize overload.
                    var init = bootstrapType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 0);

                    if (init != null)
                    {
                        try
                        {
                            init.Invoke(null, null);
                            _bootstrapSucceeded = true;
                            LogRouter.Sink?.Write($"HOST[Lifecycle:WinAppRuntimeBootstrap]: strategy=Managed asm={asmName} status=Success");
                            return;
                        }
                        catch (Exception ex)
                        {
                            LogRouter.Sink?.Write($"HOST[Lifecycle:WinAppRuntimeBootstrap]: strategy=Managed asm={asmName} status=Fail msg={ex.GetType().Name}:{ex.Message}");
                        }
                    }
                }

                // Native fallback: probe descending supported major/minor versions (1.8 → 1.7 → 1.6)
                uint[] versions = { 0x00010008u, 0x00010007u, 0x00010006u };
                foreach (var v in versions)
                {
                    int hr = MddBootstrapInitialize(v, null, 0);
                    if (hr == 0)
                    {
                        _bootstrapSucceeded = true;
                        LogRouter.Sink?.Write($"HOST[Lifecycle:WinAppRuntimeBootstrap]: strategy=Native status=Success version=0x{v:X8}");
                        break;
                    }
                    else
                    {
                        LogRouter.Sink?.Write($"HOST[Lifecycle:WinAppRuntimeBootstrap]: strategy=Native attemptVersion=0x{v:X8} status=Fail hr=0x{hr:X}");
                    }
                }

                if (!_bootstrapSucceeded)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:WinAppRuntimeBootstrap]: status=ExhaustedAttempts (managed & native) – proceeding without runtime bootstrap");
                }
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:WinAppRuntimeBootstrapError]: msg=" + ex.Message);
            }
        }

        [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int MddBootstrapInitialize(uint majorMinorVersion, string? versionTag, ulong minVersion);

        private Assembly? LoadIfExists(string file)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:WinAppRuntimeBootstrapLoadWarn]: file=" + file + " msg=" + ex.Message);
            }
            return null;
        }
        #endregion

        #region First-Chance Exception Diagnostics
        private bool _firstChanceHooked;
        private void InstallFirstChanceExceptionHook()
        {
            if (_firstChanceHooked) return;
            try
            {
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
                _firstChanceHooked = true;
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiFirstChanceHookInstalled]");
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiFirstChanceHookError]: " + ex.Message);
            }
        }

        private void OnFirstChance(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            // Reduce noise: only log once for the exact TypeInitializationException pattern we are chasing or module-level issues.
            var ex = e.Exception;
            if (ex is TypeInitializationException || ex.GetType().Name.Contains("TypeInitialization"))
            {
                LogRouter.Sink?.Write("HOST[Diag:FirstChanceTypeInit]: type=" + ex.GetType().FullName + " msg=" + ex.Message);
                if (ex.InnerException != null)
                    LogRouter.Sink?.Write("HOST[Diag:FirstChanceTypeInitInner]: inner=" + ex.InnerException.GetType().FullName + " msg=" + ex.InnerException.Message);
            }
            else if (ex.GetType().FullName == "System.DllNotFoundException" || ex.GetType().FullName == "System.IO.FileNotFoundException")
            {
                LogRouter.Sink?.Write("HOST[Diag:FirstChanceLoad]: type=" + ex.GetType().Name + " msg=" + ex.Message);
            }
        }
        #endregion

        #region Assembly Resolve Diagnostics
        private void InstallAssemblyResolveHook()
        {
            if (_assemblyResolveHooked) return;
            AppContext.SetSwitch("System.Reflection.AssemblyLoadContext.EnableActivityTracking", true);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _assemblyResolveHooked = true;
            LogRouter.Sink?.Write("HOST[Lifecycle:MauiAssemblyResolveHookInstalled]");
        }

        private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                var name = new AssemblyName(args.Name).Name ?? "";
                if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                    return null;

                LogRouter.Sink?.Write("HOST[Lifecycle:MauiAssemblyResolve]: name=" + name);

                // Already loaded?
                var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                {
                    try { return a.GetName().Name == name; } catch { return false; }
                });
                if (loaded != null)
                {
                    LogRouter.Sink?.Write("HOST[Lifecycle:MauiAssemblyResolveHit]: strategy=AlreadyLoaded name=" + name);
                    return loaded;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates =
                {
                    Path.Combine(baseDir, name + ".dll"),
                    Path.Combine(baseDir, "runtimes", "win-x64", "native", name + ".dll")
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            var asm = Assembly.LoadFrom(path);
                            LogRouter.Sink?.Write("HOST[Lifecycle:MauiAssemblyResolveHit]: strategy=LoadFrom path=" + path);
                            return asm;
                        }
                        catch (Exception ex)
                        {
                            LogRouter.Sink?.Write("HOST[Lifecycle:MauiAssemblyResolveLoadError]: path=" + path + " msg=" + ex.Message);
                        }
                    }
                }

                LogRouter.Sink?.Write("HOST[Lifecycle:MauiAssemblyResolveMiss]: name=" + name);
                return null;
            }
            catch (Exception ex)
            {
                LogRouter.Sink?.Write("HOST[Lifecycle:MauiAssemblyResolveHandlerError]: " + ex.Message);
                return null;
            }
        }
        #endregion
    }
}
