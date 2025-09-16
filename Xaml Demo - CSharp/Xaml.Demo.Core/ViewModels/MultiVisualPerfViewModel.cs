using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Xaml_Demo.Surfaces;

namespace Xaml_Demo.ViewModels
{
    /// <summary>
    /// Moved from MAUI project to Core. Orchestrates a large collection of color items
    /// and selection operations; surface ordering informs host layout.
    /// </summary>
    public sealed class MultiVisualPerfViewModel : BaseViewModel, ISelectable
    {
        public ObservableCollection<PerfItemViewModel> Items { get; } = new ObservableCollection<PerfItemViewModel>();

        // Selection membership (collection-level semantics).
        public HashSet<PerfItemViewModel> SelectedItems { get; } = new HashSet<PerfItemViewModel>();
        private int _selectionVersion;
        public int SelectionVersion
        {
            get => _selectionVersion;
            private set => SetProperty(ref _selectionVersion, value);
        }
        public int SelectedCount => SelectedItems.Count;

        public MultiVisualPerfViewModel()
        {
            GenerateSpectrum(1000);
        }

        /// <summary>
        /// Order in which surfaces should be materialized by the host view (Stage 1).
        /// Enum now lives in Core; host (MAUI / future WPF) can read this ordering.
        /// </summary>
        public IReadOnlyList<FrameworkSurfaceKind> SurfaceOrder { get; } =
            new[] { FrameworkSurfaceKind.MauiCollection, FrameworkSurfaceKind.WinUIListView };

        // Generates a hue spectrum of count entries. Hue advances by 1/count per item.
        private void GenerateSpectrum(int count)
        {
            Items.Clear();
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                double h = (double)i / count * .5; // limit to 50% of spectrum
                var color = Color.FromHsla(h, 0.7, 0.5);
                Items.Add(new PerfItemViewModel(color));
            }

            // Reset selection when regenerating items
            if (SelectedItems.Count > 0)
            {
                SelectedItems.Clear();
                BumpSelectionVersion();
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

        // ISelectable implementation (collection-level).
        void ISelectable.SetSelected(bool value)
        {
            if (value)
            {
                // Select all items
                ApplySelection(Items, true);
            }
            else
            {
                if (SelectedItems.Count > 0)
                {
                    // Deselect all currently selected items (snapshot first since we mutate the set)
                    ApplySelection(SelectedItems.ToList(), false);
                }
            }
        }

        /// <summary>
        /// Apply selection or deselection to an arbitrary set of target item VMs.
        /// Maintains authoritative HashSet membership and raises per-item Replace notifications
        /// to force template re-evaluation without rebuilding the whole list.
        /// </summary>
        public void ApplySelection(IEnumerable<PerfItemViewModel> targets, bool select)
        {
            var changed = new List<PerfItemViewModel>();

            if (select)
            {
                foreach (var vm in targets)
                {
                    if (SelectedItems.Add(vm))
                        changed.Add(vm);
                }
            }
            else
            {
                foreach (var vm in targets)
                {
                    if (SelectedItems.Remove(vm))
                        changed.Add(vm);
                }
            }

            if (changed.Count > 0)
            {
                RefreshItems(changed);
                BumpSelectionVersion();
            }
        }

        /// <summary>
        /// Raises per-item Replace collection change notifications by re-assigning the same instance
        /// back into the ObservableCollection (ObservableCollection.SetItem always fires Replace).
        /// </summary>
        private void RefreshItems(IEnumerable<PerfItemViewModel> changed)
        {
            foreach (var vm in changed)
            {
                int idx = Items.IndexOf(vm);
                if (idx >= 0)
                {
                    Items[idx] = vm;
                }
            }
        }

        bool ISelectable.IsSelected(object candidate)
            => candidate is PerfItemViewModel p && SelectedItems.Contains(p);

        public void ToggleSelectAll()
        {
            if (SelectedItems.Count < Items.Count)
                ((ISelectable)this).SetSelected(true);
            else
                ((ISelectable)this).SetSelected(false);
        }

        private void BumpSelectionVersion()
        {
            SelectionVersion++;
            OnPropertyChanged(nameof(SelectedCount));
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
}
