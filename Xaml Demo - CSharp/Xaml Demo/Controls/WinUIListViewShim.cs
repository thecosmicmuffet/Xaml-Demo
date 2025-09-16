using System.Collections;
using Microsoft.Maui;

namespace Xaml_Demo.Controls;

/// <summary>
/// Cross-platform shim control intended to host a native WinUI ListView on Windows,
/// allowing experimentation with alternative rendering paths while keeping the same
/// layout and binding surface in MAUI.
/// Non-Windows platforms currently provide a stub implementation.
/// </summary>
public class WinUIListViewShim : View
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(WinUIListViewShim),
            default(IEnumerable),
            propertyChanged: OnItemsSourceChanged);

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is WinUIListViewShim shim)
        {
            shim.Handler?.UpdateValue(nameof(ItemsSource));
        }
    }
}
