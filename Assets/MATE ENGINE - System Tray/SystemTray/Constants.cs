using System;

namespace Utils
{
    public static partial class TrayIcon
    {
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;

        // private const uint WM_DESTROY = 0x0002;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_USER = 0x0400;
        private const uint WM_APP = 0x8000;

        private const uint TRAY_ICON_MESSAGE = WM_APP + 1;

        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONUP = 0x0205;

        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        private const uint MF_STRING = 0x00000000;
        private const uint MF_POPUP = 0x00000010;
        private const uint MF_BYPOSITION = 0x00000400;
        private const uint MF_SEPARATOR = 0x00000800;

        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_LEFTBUTTON = 0x0000;
        private const uint TPM_BOTTOMALIGN = 0x0020;
        // private const uint TPM_NONOTIFY = 0x0080;
        // private const uint TPM_RETURNCMD = 0x0100;

        private const uint NIIF_NONE = 0x00000000;
        private const uint NIIF_INFO = 0x00000001;
        private const uint NIIF_WARNING = 0x00000002;
        private const uint NIIF_ERROR = 0x00000003;
        // private const uint NIIF_USER = 0x00000004;
        private const uint NIIF_NOSOUND = 0x00000010;
        // private const uint NIIF_LARGE_ICON = 0x00000020;
        // private const uint NIIF_RESPECT_QUIET_TIME = 0x00000080;
        // private const uint NIIF_ICON_MASK = 0x0000000F;

        public const string LEFT_CLICK = "LeftClick";
        public const string SEPARATOR = "Separator";
    }
}
