using System;

namespace Xaml_Demo.Surfaces
{
    /// <summary>
    /// Lightweight wrapper for a native embedding handle (HWND, XAML Island handle, etc.).
    /// Ownership: if Owned = true the surface abstraction is responsible for destroying
    /// the handle during Dispose / teardown; otherwise the host manages lifetime.
    /// </summary>
    public readonly struct NativeHandleRef
    {
        public IntPtr Handle { get; }
        public bool Owned { get; }
        public string? Description { get; }

        public NativeHandleRef(IntPtr handle, bool owned = false, string? description = null)
        {
            Handle = handle;
            Owned = owned;
            Description = description;
        }

        public bool IsNull => Handle == IntPtr.Zero;

        public override string ToString() =>
            $"{Handle.ToString("X")}(Owned={Owned}{(Description is null ? string.Empty : $", {Description}")})";
    }

    /// <summary>
    /// Categorizes why a surface signaled invalidation.
    /// </summary>
    public enum SurfaceInvalidationKind
    {
        Unknown = 0,
        DataChanged,
        VisualChanged,
        LayoutChanged,
        Disposed
    }

    /// <summary>
    /// Event args raised when a surface invalidates itself (requesting host attention).
    /// </summary>
    public sealed class SurfaceInvalidatedEventArgs : EventArgs
    {
        public SurfaceInvalidationKind Kind { get; }
        public string? Detail { get; }

        public SurfaceInvalidatedEventArgs(SurfaceInvalidationKind kind, string? detail = null)
        {
            Kind = kind;
            Detail = detail;
        }

        public override string ToString() => $"{Kind}{(Detail is null ? string.Empty : $": {Detail}")}";
    }
}
