using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Xaml_Demo.Converters
{
    /// <summary>
    /// Converts a boolean (Selected) into a DataTemplate. Demonstrates an alternate
    /// approach to selection-driven template swapping without a DataTemplateSelector.
    /// </summary>
    public sealed class SelectionToTemplateConverter : IValueConverter
    {
        public DataTemplate? SelectedTemplate { get; set; }
        public DataTemplate? UnselectedTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSelected = value is bool b && b;
            return isSelected
                ? (SelectedTemplate ?? UnselectedTemplate ?? new DataTemplate(() => new Label { Text = "[No Template]" }))
                : (UnselectedTemplate ?? SelectedTemplate ?? new DataTemplate(() => new Label { Text = "[No Template]" }));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
