namespace Xaml_Demo.Logging
{
    /// <summary>
    /// Simple logging contract placed in Core so platform/UI layers can supply an implementation.
    /// </summary>
    public interface ILogSink
    {
        void Write(string message);
    }
}
