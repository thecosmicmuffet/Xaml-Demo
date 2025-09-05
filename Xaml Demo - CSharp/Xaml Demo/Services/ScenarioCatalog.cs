using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;

namespace Xaml_Demo.Services;

public static class ScenarioCatalog
{
    private static readonly Dictionary<string, (string Title, Func<View> Create)> _map = new();

    static ScenarioCatalog()
    {
        // Register default scenarios here
        Register("MultiVisualPerf", "Multi Visual Perf", () => new Xaml_Demo.Views.Scenarios.MultiVisualPerfView());
    }

    public static void Register(string key, string title, Func<View> create)
        => _map[key] = (title, create);

    public static View? Resolve(string key)
        => _map.TryGetValue(key, out var v) ? v.Create() : null;

    public static IEnumerable<(string Key, string Title)> Items
        => _map.Select(kv => (kv.Key, kv.Value.Title));
}
