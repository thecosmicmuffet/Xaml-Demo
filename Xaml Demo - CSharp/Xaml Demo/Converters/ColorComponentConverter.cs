using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Xaml_Demo.Converters
{
    /// <summary>
    /// Extracts a single component (RGB or HSV) from a Color for slider display.
    /// ConvertBack intentionally not implemented (handled by control logic updating the composite Color).
    /// Demonstrates reusable enum-driven component extraction.
    /// </summary>
    public sealed class ColorComponentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color c && parameter is string p &&
                Enum.TryParse<ColorComponent>(p, out var comp))
            {
                switch (comp)
                {
                    case ColorComponent.Red:
                        return c.Red * 255.0;
                    case ColorComponent.Green:
                        return c.Green * 255.0;
                    case ColorComponent.Blue:
                        return c.Blue * 255.0;
                    case ColorComponent.Hue:
                        ToHsv(c, out var h, out _, out _);
                        return h;
                    case ColorComponent.Saturation:
                        ToHsv(c, out _, out var s, out _);
                        return s;
                    case ColorComponent.Value:
                        ToHsv(c, out _, out _, out var v);
                        return v;
                }
            }
            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not used; control updates Color directly from slider change events.
            return BindableProperty.UnsetValue;
        }

        private static void ToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.Red;
            double g = c.Green;
            double b = c.Blue;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            // Hue
            if (delta <= 0.0000001)
            {
                h = 0;
            }
            else if (max == r)
            {
                h = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                h = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                h = 60 * (((r - g) / delta) + 4);
            }
            if (h < 0) h += 360;

            // Saturation
            s = max == 0 ? 0 : delta / max;

            // Value
            v = max;
        }
    }

    /// <summary>
    /// Enumeration of supported color components for slider binding.
    /// </summary>
    public enum ColorComponent
    {
        Red,
        Green,
        Blue,
        Hue,
        Saturation,
        Value
    }
}
