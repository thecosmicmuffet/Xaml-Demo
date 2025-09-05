using System;

namespace Xaml_Demo.Services;

public static class NavigationHub
{
    public static event Action<string>? NavigateRequested;

    public static void RequestNavigate(string key)
        => NavigateRequested?.Invoke(key);
}
