using System;
using System.Windows.Controls;
using Xaml_Demo.Logging;

namespace Xaml.Demo.Host.Wpf.Logging
{
    /// <summary>
    /// Simple ILogSink implementation that appends lines to a WPF TextBox.
    /// Caller provides a TextBox (multiline, vertical scroll). Marshals not handled here;
    /// LogRouter can be configured to disable dispatcher marshaling or caller can invoke on UI thread.
    /// </summary>
    public sealed class WpfTextBoxLogSink : ILogSink
    {
        private readonly TextBox _textBox;
        private readonly int _maxChars;

        public WpfTextBoxLogSink(TextBox textBox, int maxChars = 200_000)
        {
            _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            _maxChars = Math.Max(10_000, maxChars);
        }

        public void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Trim if we exceed max buffer (simple strategy: drop oldest half).
            if (_textBox.Text.Length > _maxChars)
            {
                int drop = _textBox.Text.Length / 2;
                _textBox.Text = _textBox.Text.Substring(drop);
            }

            _textBox.AppendText(message + Environment.NewLine);
            _textBox.ScrollToEnd();
        }
    }
}
