using Microsoft.Maui.Graphics;

namespace Xaml_Demo.ViewModels
{
    /// <summary>
    /// Moved from MAUI project to Core. Represents an item whose color characteristics
    /// are used to explore rendering / selection performance across surface technologies.
    /// </summary>
    public sealed class PerfItemViewModel : BaseViewModel, ISelectable
    {
        private readonly Color _defaultColor;
        private Color _color;
        private bool _selected;

        public PerfItemViewModel(Color defaultColor)
        {
            _defaultColor = defaultColor;
            _color = defaultColor;
        }

        public Color DefaultColor => _defaultColor;

        public Color Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    // Also raise ColorString if Color changed (improvement over original).
                    OnPropertyChanged(nameof(ColorString));
                }
            }
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

        public bool Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        void ISelectable.SetSelected(bool value) => Selected = value;

        bool ISelectable.IsSelected(object candidate) => ReferenceEquals(candidate, this) && _selected;
    }
}
