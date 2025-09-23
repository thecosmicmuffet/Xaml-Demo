using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Xaml_Demo.ViewModels;

namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Dynamically created MAUI CollectionView surface.
    /// NOTE: Current MultiVisualPerfView still declares a static CollectionView in XAML
    /// (ItemsCollectionView) used by VisualState setters and lasso logic. This dynamic
    /// surface is a preparatory artifact for full dynamic mounting (when the static
    /// XAML CollectionView is replaced by a host placeholder).
    ///
    /// Transition plan:
    /// 1. Introduce this surface (unused yet) to define the construction pattern.
    /// 2. Replace XAML CollectionView with a ContentView host (LeftSurfaceHost).
    /// 3. Adjust VisualState setters to target a dynamic name or apply styles programmatically.
    /// 4. Update lasso + selection code to reference the dynamically created CollectionView instance.
    /// </summary>
    public sealed class MauiCollectionSurface : IRenderSurface
    {
        private readonly MultiVisualPerfViewModel _vm;
        private CollectionView? _collectionView;
        private SurfaceLifecycleState _state = SurfaceLifecycleState.Constructed;

        public MauiCollectionSurface(MultiVisualPerfViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        }

        public FrameworkSurfaceKind Kind => FrameworkSurfaceKind.MauiCollection;
        public SurfaceLifecycleState State => _state;

        public View? MauiViewHost => _collectionView;

        public event EventHandler<SurfaceInvalidatedEventArgs>? Invalidated;

        public Task InitializeAsync(object context, CancellationToken ct)
        {
            if (_collectionView != null)
            {
                if (_state != SurfaceLifecycleState.Initialized)
                {
                    var prevExisting = _state;
                    _state = SurfaceLifecycleState.Initialized;
                    SurfaceLifecycle.LogTransition(Kind, prevExisting, _state, "Re-enter InitializeAsync");
                }
                return Task.CompletedTask;
            }

            var prev = _state;
            _state = SurfaceLifecycleState.Initializing;
            SurfaceLifecycle.LogTransition(Kind, prev, _state, "Creating CollectionView");

            // Construct CollectionView similar to original XAML definition (base template only;
            // VisualState-driven template swapping will be adapted when static instance removed).
            _collectionView = new CollectionView
            {
                ItemsSource = _vm.Items
            };

            // Basic default template (mirrors PerfItemTemplate) so the surface is functional
            // even before VisualState logic is refactored for dynamic mounting.
            _collectionView.ItemTemplate = new DataTemplate(() =>
            {
                var root = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = 40 },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    Padding = 2
                };

                var colorBox = new BoxView
                {
                    CornerRadius = 4
                };
                colorBox.SetBinding(BoxView.ColorProperty, nameof(PerfItemViewModel.Color));

                var label = new Label
                {
                    FontSize = 12,
                    VerticalTextAlignment = TextAlignment.Center
                };
                label.SetBinding(Label.TextProperty, nameof(PerfItemViewModel.ColorString));

                root.Add(colorBox);
                root.Add(label, 1, 0);

                return root;
            });

            // Finish lifecycle transition
            prev = _state;
            _state = SurfaceLifecycleState.Initialized;
            SurfaceLifecycle.LogTransition(Kind, prev, _state);

            return Task.CompletedTask;
        }

        public Task<NativeHandleRef?> GetEmbedHandleAsync(CancellationToken ct)
            => Task.FromResult<NativeHandleRef?>(null);

        private void RaiseInvalidated(SurfaceInvalidationKind kind, string? detail = null) =>
            Invalidated?.Invoke(this, new SurfaceInvalidatedEventArgs(kind, detail));
    }
}
