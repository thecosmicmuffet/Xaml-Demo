namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Enumerates the framework / technology kinds for render surfaces.
    /// Lives in Core to allow both MAUI and future WPF/UWP hosts to share a single contract.
    /// </summary>
    public enum FrameworkSurfaceKind
    {
        MauiCollection,
        WinUIListView,
        UwpPlaceholder
    }
}
