using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;

namespace Xaml_Demo.ViewModels;

public sealed class MultiVisualPerfViewModel : BaseViewModel
{
    public ObservableCollection<PerfItemViewModel> Items { get; } = new();

    public MultiVisualPerfViewModel()
    {
        GenerateSpectrum(1000);
    }

    // Generates a hue spectrum of count entries. Hue advances by 1/count per item.
    private void GenerateSpectrum(int count)
    {
        Items.Clear();
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            double h = (double)i / count; // hue 0..(count-1)/count
            // Saturation & Lightness chosen for vivid but not overly bright colors
            var color = Color.FromHsla(h, 0.7, 0.5);
            Items.Add(new PerfItemViewModel(color));
        }
    }

    public int SwapColors()
    {
        int n = Items.Count;
        if (n == 0) return 0;

        int changeCount = 0;

        if (Items[0].Color != Items[0].DefaultColor)
        {
            // Already swapped, reset to default colors
            foreach (var item in Items)
            {
                if (item.Color != item.DefaultColor)
                {
                    item.ResetColor();
                    changeCount++;
                }
            }
        }
        else
        {
            // Swap colors front-to-back, or black/white if same color
            for (int i = 0; i < n / 2; i++)
            {
                var vm1 = Items[i];
                var vm2 = Items[n - 1 - i];
                if (vm1.Color == vm2.Color)
                {
                    vm1.Color = Colors.Black;  // ensure change if same
                    changeCount++;
                    vm2.Color = Colors.White;
                    changeCount++;
                }
                else
                {
                    var temp = vm1.GetColor();
                    vm1.Color = vm2.GetColor();
                    changeCount++;
                    vm2.Color = temp;
                    changeCount++;
                }
            }

            // If odd number of items, reset the middle one to its default color
            if ((n % 2) != 0)
            {
                var mid = Items[n / 2];
                if (mid.Color != mid.DefaultColor)
                {
                    mid.ResetColor();
                    changeCount++;
                }
            }
        }

        return changeCount;
    }

    public async Task<bool> AwaitColorChangesAsync(TimeSpan timeout)
    {
        if (Items.Count == 0) return true;

        var tcs = new TaskCompletionSource<bool>();
        int seen = 0;

        void Handler(object? s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PerfItemViewModel.Color))
            {
                if (Interlocked.Increment(ref seen) == Items.Count)
                {
                    tcs.TrySetResult(true);
                }
            }
        }

        foreach (var item in Items)
            item.PropertyChanged += Handler;

        SwapColors();

        using var cts = new CancellationTokenSource(timeout);
        using (cts.Token.Register(() => tcs.TrySetResult(false)))
        {
            try
            {
                var result = await tcs.Task.ConfigureAwait(false);
                return result;
            }
            finally
            {
                foreach (var item in Items)
                    item.PropertyChanged -= Handler;
            }
        }
    }
}
