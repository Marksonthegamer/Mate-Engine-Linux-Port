using System;
using System.Runtime.InteropServices;

namespace Utils
{
    public static partial class TrayIcon
    {
        private static class WinAPI
        {
            // Shell
            [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NOTIFYICONDATA lpData);


            // User32 - Windows, Messages, Menus, Icons
            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool DestroyWindow(IntPtr hWnd);

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

            [DllImport("user32.dll")]
            public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool DestroyIcon(IntPtr hIcon);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr CreatePopupMenu();

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

            [DllImport("user32.dll")]
            public static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams); //hWnd must own menu

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool DestroyMenu(IntPtr hMenu);

            [DllImport("user32.dll")]
            public static extern bool GetCursorPos(out POINT lpPoint);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateWindowEx(
                uint dwExStyle, string lpClassName, string lpWindowName,
                uint dwStyle, int x, int y, int nWidth, int nHeight,
                IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam
            );


            // GDI - Bitmaps, Icons
            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);

            [DllImport("gdi32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteObject(IntPtr hObject);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr CreateIconIndirect([In] ref ICONINFO piconinfo);
        }

        // Delegate Definition
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
