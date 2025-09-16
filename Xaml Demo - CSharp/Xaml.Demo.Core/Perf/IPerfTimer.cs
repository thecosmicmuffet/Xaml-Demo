namespace Xaml_Demo.Perf
{
    /// <summary>
    /// Lightweight performance timer abstraction so host/platform layers
    /// can supply timing without coupling Core to a specific implementation.
    /// </summary>
    public interface IPerfTimer
    {
        void Start(string scopeName);
        void Stop();
        double ElapsedMilliseconds { get; }
    }
}
