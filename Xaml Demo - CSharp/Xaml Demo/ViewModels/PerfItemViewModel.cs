using Microsoft.Maui.Graphics;

namespace Xaml_Demo.ViewModels;

public sealed class PerfItemViewModel : BaseViewModel
{
    private readonly Color _defaultColor;
    private Color _color;

    public PerfItemViewModel(Color defaultColor)
    {
        _defaultColor = defaultColor;
        _color = defaultColor;
    }

    public Color DefaultColor => _defaultColor;

    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public string ColorString
    {
        get
        {
            // Produce #RRGGBB (ignore alpha for display)
            byte r = (byte)(_color.Red * 255.0);
            byte g = (byte)(_color.Green * 255.0);
            byte b = (byte)(_color.Blue * 255.0);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }

    public Color GetColor() => _color;

    public void ResetColor() => Color = _defaultColor;
}
