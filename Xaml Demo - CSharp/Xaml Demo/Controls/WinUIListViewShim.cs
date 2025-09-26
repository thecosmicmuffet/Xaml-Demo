using System.Collections;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

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

    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(WinUIListViewShim),
            default(DataTemplate),
            propertyChanged: OnItemTemplateChanged);

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    private static void OnItemTemplateChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is WinUIListViewShim shim)
        {
            shim.Handler?.UpdateValue(nameof(ItemTemplate));
        }
    }
}
