using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Utils
{
    public static partial class TrayIcon
    {
        private static bool _init = false;
        private static string windowClassName;

        private static NOTIFYICONDATA notifyIconData;
        private static IntPtr hIcon;
        private static IntPtr messageWindowHandle;

        private static Dictionary<string, Action> MenuActions;
        private static Dictionary<uint, string> ActionMappings;
        private static Action OnLeftClick;

        private static WndProcDelegate wndProcDelegate;
        public static Func<List<(string, Action)>> OnBuildMenu;


        /// <summary>Create a System Tray Icon</summary>
        /// <param name="appName">An internal classifier (not visible)</param>
        /// <param name="tooltip">The string that shows up when hovering the icon</param>
        /// <param name="iconTexture">The texture for the icon (16x16 is recommend)</param>
        /// <param name="actions">List of menu items when clicking on the icon</param>
        public static void Init(string appName, string tooltip, Texture2D iconTexture, List<(string, Action)> actions = null)
        {
#if !UNITY_STANDALONE_WIN
            throw new NotImplementedException("These features are only avaliable on Windows...");
#endif

            if (_init)
            {
                Debug.LogError("Init can only be called once...");
                return;
            }

            if (string.IsNullOrEmpty(appName))
            {
                Debug.LogError("A title for the application is required...");
                return;
            }

            if (string.IsNullOrEmpty(tooltip))
            {
                Debug.LogError("A description when hovered is required...");
                return;
            }

            if (iconTexture == null || !iconTexture.isReadable)
            {
                Debug.LogError("Texture2D with Read/Write permission is required...");
                return;
            }

            // 0. Setup Environment
            windowClassName = appName;
            ProcessMenuActions(actions);

            // 1. Create HICON
            hIcon = CreateHIconFromTexture2D(ref iconTexture);
            if (hIcon == IntPtr.Zero)
            {
                Debug.LogError("Failed to create icon...");
                return;
            }

            // 2. Create Hidden Window for Messages
            bool success = CreateMessageWindow();
            if (!success)
            {
                Debug.LogError("Failed to create message window");
                CleanupResources();
                return;
            }

            // 3. Prepare NOTIFYICONDATA
            notifyIconData = new NOTIFYICONDATA()
            {
                cbSize = (uint)Marshal.SizeOf(notifyIconData),
                hWnd = messageWindowHandle,
                uID = GetUniqueID(),
                uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE,
                uCallbackMessage = TRAY_ICON_MESSAGE,
                hIcon = hIcon,
                szTip = tooltip
            };

            // 4. Add the icon
            success = WinAPI.Shell_NotifyIcon(NIM_ADD, ref notifyIconData);
            if (success)
            {
                _init = true;
#if UNITY_EDITOR
                Debug.Log("Successfully added System Tray Icon");
#endif
                Application.quitting += CleanupResources;
            }
            else
            {
                Debug.LogError($"Failed to add system tray icon. Error: {Marshal.GetLastWin32Error()}");
                CleanupResources();
                return;
            }
        }

        private static bool CreateMessageWindow()
        {
            IntPtr hInstance = WinAPI.GetModuleHandle(null);
            if (hInstance == IntPtr.Zero) return false;

            wndProcDelegate = new WndProcDelegate(WndProc);

            var wc = new WNDCLASSEX()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                lpszClassName = windowClassName,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                hInstance = hInstance,
                style = 0,
                hIcon = IntPtr.Zero,
                hIconSm = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                cbClsExtra = 0,
                cbWndExtra = 0
            };

            ushort classAtom = WinAPI.RegisterClassEx(ref wc);
            if (classAtom == 0)
            {
                Debug.LogError($"RegisterClassEx Failed. Error: {Marshal.GetLastWin32Error()}");
                return false;
            }

            messageWindowHandle = WinAPI.CreateWindowEx(
                0,
                windowClassName,
                windowClassName,
                0,
                0, 0, 0, 0,
                HWND_MESSAGE,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero
            );

            if (messageWindowHandle == IntPtr.Zero)
            {
                Debug.LogError($"CreateWindowEx Failed. Error: {Marshal.GetLastWin32Error()}");
                WinAPI.UnregisterClass(windowClassName, hInstance);
                return false;
            }

#if UNITY_EDITOR
            Debug.Log("Successfully created Message Window");
#endif
            return true;
        }

        private static void ShowContextMenu()
        {
            if (!WinAPI.GetCursorPos(out POINT pt))
                return;

            IntPtr hMenu = WinAPI.CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            var menuEntries = OnBuildMenu != null ? OnBuildMenu() : null;

            MenuActions = new Dictionary<string, Action>();
            ActionMappings = new Dictionary<uint, string>();
            uint commandId = 1000;

            if (menuEntries != null)
            {
                foreach (var entry in menuEntries)
                {
                    if (entry.Item1 == Utils.TrayIcon.SEPARATOR)
                    {
                        WinAPI.AppendMenu(hMenu, MF_SEPARATOR, 0, null);
                    }
                    else
                    {
                        WinAPI.AppendMenu(hMenu, MF_STRING, commandId, entry.Item1);
                        MenuActions[entry.Item1] = entry.Item2;
                        ActionMappings[commandId] = entry.Item1;
                        commandId++;
                    }
                }
            }

            WinAPI.SetForegroundWindow(messageWindowHandle);
            WinAPI.TrackPopupMenuEx(hMenu, TPM_LEFTALIGN | TPM_BOTTOMALIGN | TPM_LEFTBUTTON, pt.X, pt.Y, messageWindowHandle, IntPtr.Zero);
            WinAPI.DestroyMenu(hMenu);
        }


        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case TRAY_ICON_MESSAGE:
                    switch ((uint)lParam)
                    {
                        case WM_LBUTTONUP:  // Left Click Tray Icon
                            OnLeftClick?.Invoke();
                            break;

                        case WM_RBUTTONUP:  // Right Click Tray Icon
                            ShowContextMenu();
                            break;
                    }
                    return IntPtr.Zero;

                case WM_COMMAND:
                    uint commandId = (uint)wParam & 0xFFFF;
                    MenuActions[ActionMappings[commandId]]?.Invoke();
                    return IntPtr.Zero;

                default:
                    // default window procedure
                    return WinAPI.DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }

        private static void CleanupResources()
        {
            IntPtr hInstance = WinAPI.GetModuleHandle(null);

            if (_init && messageWindowHandle != IntPtr.Zero)
            {
                bool success = WinAPI.Shell_NotifyIcon(NIM_DELETE, ref notifyIconData);
                if (!success)
                    Debug.LogWarning("Failed to delete notifyIconData");
            }

            if (hIcon != IntPtr.Zero)
            {
                WinAPI.DestroyIcon(hIcon);
                hIcon = IntPtr.Zero;
            }

            if (messageWindowHandle != IntPtr.Zero)
            {
                WinAPI.DestroyWindow(messageWindowHandle);
                messageWindowHandle = IntPtr.Zero;
            }

            if (hInstance != IntPtr.Zero)
                WinAPI.UnregisterClass(windowClassName, hInstance);

            wndProcDelegate = null;

#if UNITY_EDITOR
            Debug.Log("Cleaned up resources for System Tray Icon");
#endif
        }
    }
}
