using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Barabulka2
{
    /// <summary>
    /// Включает/выключает "прозрачность для кликов" у WPF-окна через WinAPI.
    /// Работает поверх AllowsTransparency=true (WS_EX_LAYERED уже выставлен WPF-ом сам).
    /// </summary>
    public static class ClickThroughHelper
    {
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x20;
        private const long WS_EX_LAYERED = 0x80;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);


        private static long GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex).ToInt64()
                : GetWindowLong32(hWnd, nIndex);
        }

        private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, long newValue)
        {
            if (IntPtr.Size == 8)
                SetWindowLongPtr64(hWnd, nIndex, new IntPtr(newValue));
            else
                SetWindowLong32(hWnd, nIndex, unchecked((int)newValue));
        }

        /// <summary>
        /// enabled = true -> окно полностью прозрачно для мыши, все клики уходят на рабочий стол/окна под ним.
        /// enabled = false -> окно снова ловит клики как обычно (например, чтобы открыть настройки через саму рыбу).
        /// Вызывать после того, как у окна уже есть Win32-хэндл (после Loaded/SourceInitialized).
        /// </summary>
        public static void SetClickThrough(Window window, bool enabled)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return; // хэндл ещё не создан - рано

            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            exStyle = enabled
                ? exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED
                : exStyle & ~WS_EX_TRANSPARENT;

            SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);
        }
    }
}
