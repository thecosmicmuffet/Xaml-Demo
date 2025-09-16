using System;

namespace Xaml_Demo.ViewModels
{
    /// <summary>
    /// General selection contract used by the template selector.
    /// MultiVisualPerfViewModel supplies collection-level semantics (membership in a HashSet).
    /// PerfItemViewModel supplies per-item semantics via an internal boolean.
    /// </summary>
    public interface ISelectable
    {
        /// <summary>Set selection state on this selectable scope (item or whole set).</summary>
        void SetSelected(bool value);

        /// <summary>
        /// Returns true if the supplied candidate object should be considered selected
        /// within this selectable scope.
        /// </summary>
        bool IsSelected(object candidate);
    }
}
