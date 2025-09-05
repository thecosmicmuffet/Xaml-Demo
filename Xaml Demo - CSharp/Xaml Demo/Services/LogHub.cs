using System;
using System.ComponentModel;
using System.Text;
using Microsoft.Maui.ApplicationModel;

namespace Xaml_Demo.Services;

public sealed class LogHub : INotifyPropertyChanged
{
    private static readonly Lazy<LogHub> _instance = new(() => new LogHub());
    public static LogHub Instance => _instance.Value;

    private readonly object _sync = new();
    private readonly StringBuilder _buffer = new();

    private string _logText = string.Empty;
    public string LogText
    {
        get => _logText;
        private set
        {
            if (_logText == value) return;
            _logText = value;
            PropertyChanged?.Invoke(this, new(nameof(LogText)));
        }
    }

    private DateTime? _timerStart;
    public static TimeSpan Elapsed => Instance._timerStart.HasValue ? DateTime.Now - Instance._timerStart.Value : TimeSpan.Zero;
    public static void StartTimer()
    {
        Instance._timerStart = DateTime.Now;
        Write($"Timer started: {Instance._timerStart:HH:mm:ss.fff}");
    }
    public static void StopTimer()
    {
        Write($"Elapsed time: {Elapsed.TotalMilliseconds:N0} ms");
        Instance._timerStart = null;
    }

    private LogHub() { }

    public static void Write(string message) => Instance.Append(message);

    public void Append(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (_sync)
        {
            _buffer.AppendLine(line);
            var text = _buffer.ToString();
            MainThread.BeginInvokeOnMainThread(() => LogText = text);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _buffer.Clear();
            MainThread.BeginInvokeOnMainThread(() => LogText = string.Empty);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
