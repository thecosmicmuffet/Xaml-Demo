using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Xaml_Demo.Services;

        /// <summary>
        /// Attached property that reports the first time a visual element (typically the root
        /// of a DataTemplate) receives a non-null BindingContext, allowing measurement of
        /// realization latency when swapping DataTemplates or VisualStates.
        /// Usage:
        ///   xmlns:svc="clr-namespace:Xaml_Demo.Services"
        /// In a DataTemplate root element:
        ///   <Grid svc:FirstBindTracker.Enable="True" ...> ... </Grid>
        /// Optional command callback:
        ///   svc:FirstBindTracker.Command="{Binding SomeCommand}"
        /// After the first non-null BindingContext is observed the handler detaches to avoid
        /// leaks and duplicate notifications even under virtualization reuse.
        /// </summary>
        public static class FirstBindTracker
{
    /// <summary>
    /// Fired the first time each element with Enable=True receives a non-null BindingContext.
    /// Sender is the visual element; second parameter is the BindingContext instance.
    /// </summary>
    public static event Action<VisualElement, object?>? FirstBind;

    public static readonly BindableProperty EnableProperty =
        BindableProperty.CreateAttached(
            "Enable",
            typeof(bool),
            typeof(FirstBindTracker),
            false,
            propertyChanged: OnEnableChanged);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.CreateAttached(
            "Command",
            typeof(ICommand),
            typeof(FirstBindTracker),
            null);

    // Internal guard so we only report once per element instance
    private static readonly BindableProperty ReportedProperty =
        BindableProperty.CreateAttached(
            "Reported",
            typeof(bool),
            typeof(FirstBindTracker),
            false);

    public static void SetEnable(BindableObject obj, bool value) => obj.SetValue(EnableProperty, value);
    public static bool GetEnable(BindableObject obj) => (bool)obj.GetValue(EnableProperty);

    public static void SetCommand(BindableObject obj, ICommand? value) => obj.SetValue(CommandProperty, value);
    public static ICommand? GetCommand(BindableObject obj) => (ICommand?)obj.GetValue(CommandProperty);

    private static void OnEnableChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement ve)
            return;

        if ((bool)newValue)
        {
            ve.BindingContextChanged += OnBindingContextChanged;
            // If BindingContext already non-null (rare but possible) handle immediately
            if (ve.BindingContext is not null)
            {
                ReportIfNeeded(ve);
            }
        }
        else
        {
            ve.BindingContextChanged -= OnBindingContextChanged;
        }
    }

    private static void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement ve)
        {
            if (ve.BindingContext is null)
                return;
            ReportIfNeeded(ve);
        }
    }

    private static void ReportIfNeeded(VisualElement ve)
    {
        if ((bool)ve.GetValue(ReportedProperty))
            return;

        ve.SetValue(ReportedProperty, true);

        try
        {
            // Fire global event
            FirstBind?.Invoke(ve, ve.BindingContext);

            // Execute attached command if present
            var cmd = GetCommand(ve);
            if (cmd != null)
            {
                if (cmd.CanExecute(ve.BindingContext))
                    cmd.Execute(ve.BindingContext);
            }
        }
        finally
        {
            // Detach to avoid unnecessary references; even if reused we only wanted first realization
            ve.BindingContextChanged -= OnBindingContextChanged;
        }
    }
}
