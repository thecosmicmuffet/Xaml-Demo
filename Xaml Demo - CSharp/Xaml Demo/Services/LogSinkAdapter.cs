using Xaml_Demo.Logging;

namespace Xaml_Demo.Services
{
    /// <summary>
    /// Adapts platform LogHub to Core ILogSink so Core ViewModels can log via LogRouter.
    /// Registered during MAUI startup (see MauiProgram).
    /// </summary>
    internal sealed class LogSinkAdapter : ILogSink
    {
        public void Write(string message) => LogHub.Write(message);
    }
}
