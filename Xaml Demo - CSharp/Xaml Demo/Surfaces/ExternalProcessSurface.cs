using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Xaml_Demo.Surfaces;
using Xaml_Demo.Services;
using Xaml_Demo.Perf;
using Xaml_Demo.Ipc;
using Xaml_Demo.Logging;

namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Out-of-process surface that launches MAUI as a standalone packaged process
    /// and communicates via IPC (named pipes). Demonstrates true cross-process embedding.
    /// </summary>
    public sealed class ExternalProcessSurface : IRenderSurface, IDisposable
    {
        private SurfaceLifecycleState _state = SurfaceLifecycleState.Constructed;
        private bool _initialized;
        private Process? _externalProcess;
        private IpcChannel? _ipcChannel;
        private IntPtr _windowHandle;
        private readonly string _pipeName;
        private CancellationTokenSource? _listenerCts;

        public ExternalProcessSurface()
        {
            // Unique pipe name per instance
            _pipeName = $"XamlDemo_Surface_{Guid.NewGuid():N}";
        }

        public FrameworkSurfaceKind Kind => FrameworkSurfaceKind.UwpPlaceholder;

        public SurfaceLifecycleState State => _state;

        public View? MauiViewHost => null; // Not hostable in-process

        public event EventHandler<SurfaceInvalidatedEventArgs>? Invalidated;

        public async Task InitializeAsync(object context, CancellationToken ct)
        {
            if (_initialized)
            {
                if (_state != SurfaceLifecycleState.Initialized)
                {
                    var prevRe = _state;
                    _state = SurfaceLifecycleState.Initialized;
                    SurfaceLifecycle.LogTransition(Kind, prevRe, _state, "Re-enter InitializeAsync");
                }
                return;
            }

            var prev = _state;
            _state = SurfaceLifecycleState.Initializing;
            SurfaceLifecycle.LogTransition(Kind, prev, _state, $"Launching external process (pipe: {_pipeName})");

            try
            {
                // Create IPC server (wait for external process to connect)
                _ipcChannel = new IpcChannel(_pipeName, isServer: true);
                _ipcChannel.MessageReceived += OnIpcMessageReceived;
                _ipcChannel.Disconnected += OnIpcDisconnected;

                // Start listening task (will wait for connection)
                var connectTask = _ipcChannel.ConnectAsync(ct);

                // Launch external MAUI process with pipe name as argument
                _externalProcess = LaunchExternalProcess(_pipeName);

                if (_externalProcess == null)
                {
                    throw new InvalidOperationException("Failed to launch external MAUI process");
                }

                // Wait for process to connect via IPC (timeout after 10 seconds)
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                await connectTask.ConfigureAwait(false);

                // Start listening for messages
                _listenerCts = new CancellationTokenSource();
                _ = Task.Run(() => _ipcChannel.StartListeningAsync(_listenerCts.Token), _listenerCts.Token);

                prev = _state;
                _state = SurfaceLifecycleState.Initialized;
                SurfaceLifecycle.LogTransition(Kind, prev, _state, $"External process connected (PID: {_externalProcess.Id})");
                _initialized = true;

                RaiseInvalidated(SurfaceInvalidationKind.DataChanged, "ExternalProcessReady");
            }
            catch (OperationCanceledException)
            {
                RaiseInvalidated(SurfaceInvalidationKind.Disposed, "Initialization canceled");
                Dispose();
                throw;
            }
            catch (Exception ex)
            {
                SurfaceLifecycle.LogTransition(Kind, _state, SurfaceLifecycleState.Constructed, 
                    $"External process launch failed: {ex.Message}");
                Dispose();
                throw;
            }
        }

        public Task<NativeHandleRef?> GetEmbedHandleAsync(CancellationToken ct)
        {
            if (_windowHandle != IntPtr.Zero)
            {
                return Task.FromResult<NativeHandleRef?>(
                    new NativeHandleRef(_windowHandle, owned: false, "ExternalMAUIWindow"));
            }

            return Task.FromResult<NativeHandleRef?>(null);
        }

        private Process? LaunchExternalProcess(string pipeName)
        {
            try
            {
                // Path to the external MAUI executable
                // This assumes the external project has been built to its standard output directory
                var externalExePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", // Navigate up from main app bin directory
                    "Xaml.Demo.External",
                    "bin", "Debug", "net10.0-windows10.0.19041.0", "win-x64",
                    "Xaml.Demo.External.exe"
                );

                var fullPath = Path.GetFullPath(externalExePath);
                
                if (!File.Exists(fullPath))
                {
                    LogRouter.Write($"[ExternalProcess] Executable not found: {fullPath}");
                    LogRouter.Write($"[ExternalProcess] Build Xaml.Demo.External project first");
                    return null;
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = fullPath,
                    Arguments = $"--pipe={pipeName}",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(fullPath)
                };

                LogRouter.Write($"[ExternalProcess] Launching: {startInfo.FileName} {startInfo.Arguments}");
                
                return Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                LogRouter.Write($"[ExternalProcess] Launch error: {ex.Message}");
                return null;
            }
        }

        private void OnIpcMessageReceived(object? sender, IpcMessage message)
        {
            try
            {
                switch (message)
                {
                    case ProcessReadyMessage ready:
                        _windowHandle = new IntPtr(ready.WindowHandle);
                        LogRouter.Write($"[ExternalProcess] Process ready, HWND: 0x{_windowHandle:X}");
                        RaiseInvalidated(SurfaceInvalidationKind.DataChanged, "WindowHandleReceived");
                        break;

                    case WindowCreatedMessage created:
                        _windowHandle = new IntPtr(created.WindowHandle);
                        LogRouter.Write($"[ExternalProcess] Window created: {created.Width}x{created.Height}, HWND: 0x{_windowHandle:X}");
                        RaiseInvalidated(SurfaceInvalidationKind.LayoutChanged, "WindowCreated");
                        break;

                    case PerfMetricMessage perf:
                        LogRouter.Write($"[ExternalProcess] PERF: {perf.MetricType} = {perf.ElapsedMs:F2}ms ({perf.ItemCount} items)");
                        break;

                    case StatusResponseMessage status:
                        LogRouter.Write($"[ExternalProcess] Status: {status.State}, Items: {status.ItemCount}, Selected: {status.SelectedCount}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogRouter.Write($"[ExternalProcess] Message handling error: {ex.Message}");
            }
        }

        private void OnIpcDisconnected(object? sender, EventArgs e)
        {
            LogRouter.Write("[ExternalProcess] IPC disconnected");
            RaiseInvalidated(SurfaceInvalidationKind.Disposed, "ProcessDisconnected");
        }

        public async Task SendCommandAsync(IpcMessage command)
        {
            if (_ipcChannel != null && _ipcChannel.IsConnected)
            {
                await _ipcChannel.SendMessageAsync(command).ConfigureAwait(false);
            }
        }

        private void RaiseInvalidated(SurfaceInvalidationKind kind, string? detail = null)
            => Invalidated?.Invoke(this, new SurfaceInvalidatedEventArgs(kind, detail));

        public void Dispose()
        {
            if (_state == SurfaceLifecycleState.Disposed)
                return;

            _listenerCts?.Cancel();
            _listenerCts?.Dispose();

            try
            {
                if (_ipcChannel != null && _ipcChannel.IsConnected)
                {
                    _ipcChannel.SendMessageAsync(new ShutdownMessage()).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch { /* Best effort */ }

            _ipcChannel?.Dispose();

            if (_externalProcess != null && !_externalProcess.HasExited)
            {
                try
                {
                    _externalProcess.Kill();
                    _externalProcess.WaitForExit(2000);
                }
                catch { /* Best effort */ }
            }

            _externalProcess?.Dispose();

            var prev = _state;
            _state = SurfaceLifecycleState.Disposed;
            SurfaceLifecycle.LogTransition(Kind, prev, _state, "External process disposed");
        }
    }
}
