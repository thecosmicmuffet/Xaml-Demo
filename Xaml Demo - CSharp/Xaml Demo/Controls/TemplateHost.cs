using System;
using Microsoft.Maui.Controls;

namespace Xaml_Demo.Controls
{
    /// <summary>
    /// Hosts dynamic DataTemplate content. The template is typically supplied via a binding
    /// (e.g. to a converter that maps a boolean Selected property to one of two templates).
    /// When the Template property changes, this control recreates its child content and
    /// applies the current Item (or BindingContext) as the BindingContext of the inflated view.
    /// </summary>
    public sealed class TemplateHost : ContentView
    {
        public static readonly BindableProperty TemplateProperty =
            BindableProperty.Create(
                nameof(Template),
                typeof(DataTemplate),
                typeof(TemplateHost),
                default(DataTemplate),
                propertyChanged: OnTemplateChanged);

        public static readonly BindableProperty ItemProperty =
            BindableProperty.Create(
                nameof(Item),
                typeof(object),
                typeof(TemplateHost),
                default(object),
                propertyChanged: OnItemChanged);

        /// <summary>
        /// The DataTemplate to realize as child content.
        /// </summary>
        public DataTemplate? Template
        {
            get => (DataTemplate?)GetValue(TemplateProperty);
            set => SetValue(TemplateProperty, value);
        }

        /// <summary>
        /// Explicit item to set as BindingContext for realized template. If null,
        /// the control's own BindingContext is used.
        /// </summary>
        public object? Item
        {
            get => GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        private static void OnTemplateChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is TemplateHost host)
            {
                host.RealizeTemplate();
            }
        }

        private static void OnItemChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is TemplateHost host)
            {
                host.UpdateChildBindingContext();
            }
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            // If Item not explicitly set, propagate BindingContext change.
            if (Item == null)
            {
                UpdateChildBindingContext();
            }
        }

        private void RealizeTemplate()
        {
            if (Template == null)
            {
                Content = null;
                return;
            }

            object created = Template.CreateContent();

            // DataTemplate.CreateContent may return a View or a nested object (e.g., View + object).
            if (created is View view)
            {
                Content = view;
            }
            else if (created is BindableObject bo && bo is View v2)
            {
                Content = v2;
            }
            else
            {
                // Fallback: wrap primitive in a Label
                Content = new Label { Text = created?.ToString() ?? "[null]" };
            }

            UpdateChildBindingContext();
        }

        private void UpdateChildBindingContext()
        {
            if (Content is BindableObject bindable)
            {
                bindable.BindingContext = Item ?? BindingContext;
            }
        }
    }
}
