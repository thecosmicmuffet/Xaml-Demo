using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Xaml.Demo.Host.Wpf.Hosting
{
    /// <summary>
    /// Minimal HwndHost wrapper that re-parents an existing HWND (MAUI / WinUI window) into WPF.
    /// It does NOT own the lifetime of the child window; DestroyWindow is never called.
    /// </summary>
    public sealed class MauiHwndHost : HwndHost
    {
        private readonly IntPtr _child;
        private IntPtr _hostHandle;

        public MauiHwndHost(IntPtr childHandle)
        {
            if (childHandle == IntPtr.Zero) throw new ArgumentNullException(nameof(childHandle));
            _child = childHandle;
            Focusable = true;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hostHandle = hwndParent.Handle;

            // Re-parent the child window
            SetParent(_child, _hostHandle);

            // Adjust styles: make child a visible child window without caption / overlapping redraw artifacts.
            var style = GetWindowLong(_child, GWL_STYLE);
            style &= ~WS_OVERLAPPED;
            style |= WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;
            SetWindowLong(_child, GWL_STYLE, style);

            var exStyle = GetWindowLong(_child, GWL_EXSTYLE);
            SetWindowLong(_child, GWL_EXSTYLE, exStyle);

            // Initial size
            ResizeChild(ActualWidth, ActualHeight);

            return new HandleRef(this, _child);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            // We do not destroy the child; simply detach (optional SetParent to 0).
            try { SetParent(_child, IntPtr.Zero); } catch { /* ignore */ }
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            ResizeChild(rcBoundingBox.Width, rcBoundingBox.Height);
        }

        private void ResizeChild(double width, double height)
        {
            if (_child == IntPtr.Zero) return;
            int w = Math.Max(0, (int)Math.Round(width));
            int h = Math.Max(0, (int)Math.Round(height));
            MoveWindow(_child, 0, 0, w, h, true);
        }

        #region Win32

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const int WS_CHILD = 0x40000000;
        private const int WS_OVERLAPPED = 0x00000000;
        private const int WS_CLIPCHILDREN = 0x02000000;
        private const int WS_CLIPSIBLINGS = 0x04000000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion
    }
}
