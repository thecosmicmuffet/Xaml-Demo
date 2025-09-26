using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Dispatching;

namespace Xaml_Demo.Controls
{
    public partial class ColorRgbEditor : ContentView
    {
        public static readonly BindableProperty TargetColorProperty =
            BindableProperty.Create(
                nameof(TargetColor),
                typeof(Color),
                typeof(ColorRgbEditor),
                Colors.Black,
                BindingMode.TwoWay,
                propertyChanged: OnTargetColorChanged);

        public Color TargetColor
        {
            get => (Color)GetValue(TargetColorProperty);
            set => SetValue(TargetColorProperty, value);
        }

        IDispatcherTimer _throttle;
        Label _rLabel, _gLabel, _bLabel;

        public ColorRgbEditor()
        {
            InitializeComponent();

            _throttle = Dispatcher.CreateTimer();
            _throttle.Interval = TimeSpan.FromMilliseconds(100);
            _throttle.Tick += (s, e) =>
            {
                OnRgbChanged(this, null);
                if (_throttle.IsRunning)
                    _throttle.Stop();
            };

            InitializeLabelReferences();
            UpdateAccessibleForeground();
        }

        private void InitializeLabelReferences()
        {
            try
            {
                if (Content is Layout root && root.Children.Count > 0)
                {
                    if (root.Children[0] is Grid grid)
                    {
                        // Expected child order: BoxView, R Label, R Slider, G Label, G Slider, B Label, B Slider
                        if (grid.Children.Count >= 6)
                        {
                            _rLabel = grid.Children[1] as Label;
                            _gLabel = grid.Children[3] as Label;
                            _bLabel = grid.Children[5] as Label;
                        }
                    }
                }
            }
            catch
            {
                // Swallow – best effort lookup; not fatal if it fails.
            }
        }

        private static double Linearize(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private Color ComputeAccessibleTextColor(Color bg)
        {
            // Relative luminance per WCAG
            double luminance = 0.2126 * Linearize(bg.Red)
                             + 0.7152 * Linearize(bg.Green)
                             + 0.0722 * Linearize(bg.Blue);

            double contrastWithWhite = (1.0 + 0.05) / (luminance + 0.05);
            double contrastWithBlack = (luminance + 0.05) / 0.05;

            // Choose higher contrast (common heuristic for dynamic fg)
            return contrastWithWhite >= contrastWithBlack ? Colors.White : Colors.Black;
        }

        private void UpdateAccessibleForeground()
        {
            var fg = ComputeAccessibleTextColor(TargetColor);
            if (_rLabel != null) _rLabel.TextColor = fg;
            if (_gLabel != null) _gLabel.TextColor = fg;
            if (_bLabel != null) _bLabel.TextColor = fg;
        }

        private static void OnTargetColorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ColorRgbEditor editor)
            {
                // Slider bindings pull component values automatically; just refresh contrast fg.
                editor.UpdateAccessibleForeground();
            }
        }


        // Handles any of the three RGB sliders changing.
        private void OnRgbChanged(object sender, ValueChangedEventArgs e)
        {
            if (sender == this)
            {
                // Gather current slider values (0..255)
                double r = RedSlider.Value / 255.0;
                double g = GreenSlider.Value / 255.0;
                double b = BlueSlider.Value / 255.0;

                var current = TargetColor;
                // Preserve original alpha (default 1)
                double a = current.Alpha;

                // Avoid redundant updates
                if (Math.Abs(current.Red - r) < 0.0001 &&
                    Math.Abs(current.Green - g) < 0.0001 &&
                    Math.Abs(current.Blue - b) < 0.0001)
                {
                    return;
                }

                TargetColor = new Color((float)r, (float)g, (float)b, (float)a);
            }
            else
            {
                // Throttle rapid slider changes to a single update.
                if (!_throttle.IsRunning)
                    _throttle.Start();
            }
        }
    }
}
