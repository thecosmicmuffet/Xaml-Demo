using Microsoft.Maui.Controls;
using Xaml_Demo.ViewModels;

namespace Xaml_Demo.Views.Selectors
{
    /// <summary>
    /// Chooses between normal and selected item templates based on an external selection context (ISelectable),
    /// typically the MultiVisualPerfViewModel which tracks a HashSet of selected item view models.
    /// </summary>
    public sealed class PerfItemSelectionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? PerfItemTemplate { get; set; }
        public DataTemplate? PerfItemTemplateSelected { get; set; }

        /// <summary>
        /// The selection scope (e.g., MultiVisualPerfViewModel) providing IsSelected(candidate).
        /// </summary>
        public ISelectable? SelectionContext { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (SelectionContext != null && SelectionContext.IsSelected(item))
            {
                if (PerfItemTemplateSelected != null)
                    return PerfItemTemplateSelected;
            }
            return PerfItemTemplate ?? PerfItemTemplateSelected ?? new DataTemplate(() => new ContentView());
        }
    }
}
