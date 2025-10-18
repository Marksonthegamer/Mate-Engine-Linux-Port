using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Utils
{
    public static partial class TrayIcon
    {
        public enum ToolTipIcon
        {
            None,
            Info,
            Warning,
            Error
        }

        /// <summary>Displays a balloon notification from the system tray icon</summary>
        /// <param name="title">The title of the notification (less than 64 characters)</param>
        /// <param name="message">The text content of the notification (less than 256 characters)</param>
        /// <param name="iconType">The icon to display</param>
        /// <param name="useSound">Play the notification sound</param>
        public static void ShowBalloonTip(string title, string message, ToolTipIcon iconType, bool useSound = true)
        {
            if (!_init || messageWindowHandle == IntPtr.Zero)
            {
                Debug.LogError("TrayIcon is not initialized yet...");
                return;
            }

            notifyIconData.uFlags = NIF_INFO;
            notifyIconData.szInfoTitle = TruncateString(title, 64);
            notifyIconData.szInfo = TruncateString(message, 256);

            switch (iconType)
            {
                case ToolTipIcon.None: notifyIconData.dwInfoFlags = NIIF_NONE; break;
                case ToolTipIcon.Info: notifyIconData.dwInfoFlags = NIIF_INFO; break;
                case ToolTipIcon.Warning: notifyIconData.dwInfoFlags = NIIF_WARNING; break;
                case ToolTipIcon.Error: notifyIconData.dwInfoFlags = NIIF_ERROR; break;
            }

            if (!useSound)
                notifyIconData.dwInfoFlags |= NIIF_NOSOUND;

            bool success = WinAPI.Shell_NotifyIcon(NIM_MODIFY, ref notifyIconData);
            if (!success)
                Debug.LogError($"Shell_NotifyIcon Failed. Error: {Marshal.GetLastWin32Error()}");

            notifyIconData.uFlags &= ~NIF_INFO;
            notifyIconData.dwInfoFlags = NIIF_NONE;
            notifyIconData.szInfoTitle = "";
            notifyIconData.szInfo = "";
        }
    }
}
