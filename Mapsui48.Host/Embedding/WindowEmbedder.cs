using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Mapsui48.Host.Embedding
{
    public static class WindowEmbedder
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        private const int GWL_STYLE = -16;
        private const long WS_CHILD = 0x40000000L;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLongPtr32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        public static void AttachTo(Form form, IntPtr parentHwnd)
        {
            IntPtr hostHwnd = form.Handle;

            // 1. Reparent
            SetParent(hostHwnd, parentHwnd);

            // 2. Remove window chrome and make it a child
            long style = GetWindowLongPtr(hostHwnd, GWL_STYLE).ToInt64();
            style = (style & ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME)) | WS_CHILD;
            SetWindowLongPtr(hostHwnd, GWL_STYLE, new IntPtr(style));

            // 3. Move window to fill parent initially
            UpdateSize(hostHwnd, parentHwnd);
        }

        public static void UpdateSize(Form form, IntPtr parentHwnd)
        {
            IntPtr hostHwnd = form.Handle;
            if (hostHwnd != IntPtr.Zero && parentHwnd != IntPtr.Zero)
            {
                UpdateSize(hostHwnd, parentHwnd);
            }
        }

        private static void UpdateSize(IntPtr hostHwnd, IntPtr parentHwnd)
        {
            if (GetClientRect(parentHwnd, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                MoveWindow(hostHwnd, 0, 0, width, height, true);
            }
        }
    }
}
