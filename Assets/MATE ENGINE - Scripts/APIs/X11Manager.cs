using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using Debug = UnityEngine.Debug;

namespace X11
{
    public class X11Manager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public static X11Manager Instance;
        private Canvas canvas;

        public IntPtr Display
        {
            get { return _display; }
        }

        public IntPtr RootWindow
        {
            get { return _rootWindow; }
        }

        public IntPtr UnityWindow
        {
            get { return _unityWindow; }
        }

        #region API

        private IntPtr _display;
        private IntPtr _rootWindow;
        private IntPtr _unityWindow;

        private const int XaCardinal = 6;
        private const int XaAtom = 4;
        private const int IsViewable = 2;

        private const string LibX11 = "libX11.so.6";
        private const string LibXExt = "libXext.so.6";
        private const string LibXRender = "libXrender.so.1";
        private const string LibXDamage = "libXdamage.so.1";

        [DllImport(LibX11)]
        private static extern IntPtr XOpenDisplay(string displayName);

        [DllImport(LibX11)]
        private static extern void XCloseDisplay(IntPtr display);

        [DllImport(LibX11)]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(LibX11)]
        private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

        [DllImport(LibX11)]
        private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
            long longOffset, long longLength, bool delete, IntPtr reqType,
            out IntPtr actualTypeReturn, out int actualFormatReturn,
            out ulong nItemsReturn, out ulong bytesAfterReturn, out IntPtr propReturn);

        [DllImport(LibX11)]
        private static extern int XGetWindowProperty(IntPtr display, IntPtr w, IntPtr property, long longOffset,
            long longLength, int delete, IntPtr reqType, out IntPtr actualTypeReturn, out int actualFormatReturn,
            out IntPtr nItemsReturn, out IntPtr bytesAfterReturn, [Out] out IntPtr propReturn);

        [DllImport(LibX11)]
        private static extern int XGetGeometry(IntPtr display, IntPtr w, out IntPtr rootReturn, out int x, out int y,
            out int width, out int height, out int borderWidth, out uint depth);

        public int GetGeometry(out IntPtr rootReturn, out int x, out int y, out int width, out int height,
            out int borderWidth, out uint depth) => XGetGeometry(_display, _unityWindow, out rootReturn, out x, out y,
            out width, out height, out borderWidth, out depth);

        [DllImport(LibX11)]
        private static extern int XFree(IntPtr data);

        [DllImport(LibX11)]
        private static extern int XQueryTree(IntPtr display, IntPtr window,
            out IntPtr rootReturn, out IntPtr parentReturn,
            out IntPtr childrenReturn, out uint nChildrenReturn);

        [DllImport(LibX11)]
        private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);

        [DllImport(LibX11)]
        private static extern int XMoveWindow(IntPtr display, IntPtr window, int x, int y);

        [DllImport(LibX11)]
        private static extern int XResizeWindow(IntPtr display, IntPtr window, int width, int height);

        [DllImport(LibX11)]
        private static extern bool XQueryPointer(IntPtr display, IntPtr window, ref IntPtr windowReturn,
            ref IntPtr childReturn,
            ref int rootX, ref int rootY, ref int winX, ref int winY, ref uint mask);

        [DllImport(LibX11)]
        private static extern int XFlush(IntPtr display);

        [DllImport(LibX11)]
        private static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate,
            long eventMask, ref XClientMessageEvent eventSend);

        [DllImport(LibX11)]
        private static extern IntPtr XRootWindow(IntPtr display, int screenNumber);

        [DllImport(LibX11)]
        private static extern bool XTranslateCoordinates(IntPtr display, IntPtr srcW, IntPtr destW,
            int srcX, int srcY, out int destX, out int destY, out IntPtr child);

        [DllImport(LibX11)]
        private static extern int XGetClassHint(IntPtr display, IntPtr w, out XClassHint classHints);

        [DllImport(LibX11)]
        private static extern int XGetWMName(IntPtr display, IntPtr w, out XTextProperty textProp);

        [DllImport(LibX11)]
        private static extern int XConfigureWindow(IntPtr display, IntPtr w, uint valueMask,
            ref XWindowChanges changes);

        [DllImport(LibX11)]
        private static extern int XDisplayWidth(IntPtr display, int screen);

        [DllImport(LibX11)]
        private static extern int XDisplayHeight(IntPtr display, int screen);

        [DllImport(LibX11)]
        private static extern void XSync(IntPtr display, bool discard);

        [StructLayout(LayoutKind.Sequential)]
        private struct XClientMessageEvent
        {
            public int type;
            public IntPtr serial;
            public bool send_event;
            public IntPtr display;
            public IntPtr window;
            public IntPtr message_type;
            public int format;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public IntPtr[] data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XWindowAttributes
        {
            public int x, y;
            public int width, height;
            public int border_width;
            public int depth;
            public IntPtr visual;
            public IntPtr root;
            public int c_class;
            public int bit_gravity;
            public int win_gravity;
            public int backing_store;
            public ulong backing_planes;
            public ulong backing_pixel;
            public bool save_under;
            public IntPtr colormap;
            public bool map_installed;
            public int map_state;
            public long all_event_masks;
            public long your_event_mask;
            public long do_not_propagate_mask;
            public bool override_redirect;
            public IntPtr screen;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XClassHint
        {
            public IntPtr res_name;
            public IntPtr res_class;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XTextProperty
        {
            public IntPtr value;
            public IntPtr encoding;
            public int format;
            public ulong nItems;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XWindowChanges
        {
            public int x, y;
            public int width, height;
            public int border_width;
            public IntPtr sibling;
            public int stack_mode;
        }

        private const uint CwSibling = (1U << 5);
        private const uint CwStackMode = (1U << 4);
        private const int Below = 1;

        private bool transparentInputEnabled = false;
        private int damageEventBase;
        private IntPtr damage = IntPtr.Zero;
        private bool running = true;

        private const long StructureNotifyMask = (1L << 17);
        private const int ConfigureNotify = 22;
        private const int DestroyNotify = 17;
        private const int ShapeBounding = 0;
        private const int ShapeInput = 2;
        private const int ShapeSet = 0;
        private const int PictTypeDirect = 1;
        private const int XDamageReportNonEmpty = 3;
        private const ulong GCForeground = (1UL << 2);
        private const ulong GCBackground = (1UL << 3);
        private const int ZPixmap = 2;
        private const ulong AllPlanes = 0xFFFFFFFFFFFFFFFFUL; // For 64-bit

        [StructLayout(LayoutKind.Explicit)]
        private struct XEvent
        {
            [FieldOffset(0)] public int type;
            [FieldOffset(0)] public XAnyEvent anyEvent;
            [FieldOffset(0)] public XConfigureEvent configureEvent;
            [FieldOffset(0)] public XDestroyWindowEvent destroyWindowEvent;
            [FieldOffset(0)] public XDamageNotifyEvent damageNotifyEvent;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XAnyEvent
        {
            public int type;
            public ulong serial;
            public bool send_event;
            public IntPtr display;
            public IntPtr window;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XConfigureEvent
        {
            public int type;
            public ulong serial;
            public bool send_event;
            public IntPtr display;
            public IntPtr event_window;
            public IntPtr window;
            public int x, y;
            public int width, height;
            public int border_width;
            public IntPtr above;
            public bool override_redirect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XDestroyWindowEvent
        {
            public int type;
            public ulong serial;
            public bool send_event;
            public IntPtr display;
            public IntPtr event_window;
            public IntPtr window;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XDamageNotifyEvent
        {
            public int type;
            public ulong serial;
            public bool send_event;
            public IntPtr display;
            public IntPtr drawable;
            public IntPtr damage;
            public int level;
            public bool more;
            public ulong timestamp;
            public XRectangle area;
            public XRectangle geometry;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XRectangle
        {
            public short x, y;
            public ushort width, height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XRenderPictFormat
        {
            public IntPtr id;
            public int type;
            public int depth;
            public XRenderDirectFormat direct;
            public IntPtr colormap;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XRenderDirectFormat
        {
            public short red;
            public short redMask;
            public short green;
            public short greenMask;
            public short blue;
            public short blueMask;
            public short alpha;
            public short alphaMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XGCValues
        {
            public int function;
            public ulong plane_mask;
            public ulong foreground;
            public ulong background;
            public int line_width;
            public int line_style;
            public int cap_style;
            public int join_style;
            public int fill_style;
            public int fill_rule;
            public int arc_mode;
            public IntPtr tile;
            public IntPtr stipple;
            public int ts_x_origin;
            public int ts_y_origin;
            public IntPtr font;
            public int subwindow_mode;
            public bool graphics_exposures;
            public int clip_x_origin;
            public int clip_y_origin;
            public IntPtr clip_mask;
            public int dash_offset;
            public char dashes;
        }

        private struct Image
        {
            public byte[] data;
            public int width, height;
        }

        [DllImport(LibX11)]
        private static extern int XSelectInput(IntPtr display, IntPtr window, long eventMask);

        [DllImport(LibXDamage)]
        private static extern bool XDamageQueryExtension(IntPtr display, out int eventBase, out int errorBase);

        [DllImport(LibXDamage)]
        private static extern IntPtr XDamageCreate(IntPtr display, IntPtr drawable, int level);

        [DllImport(LibXDamage)]
        private static extern void XDamageDestroy(IntPtr display, IntPtr damage);

        [DllImport(LibXDamage)]
        private static extern void XDamageSubtract(IntPtr display, IntPtr damage, IntPtr repair, IntPtr parts);

        [DllImport(LibXExt)]
        private static extern void XShapeCombineMask(IntPtr display, IntPtr window, int destKind, int xOff, int yOff,
            IntPtr mask, int op);

        [DllImport(LibXRender)]
        private static extern IntPtr XRenderFindVisualFormat(IntPtr display, IntPtr visual);

        [DllImport(LibX11)]
        private static extern IntPtr XCreatePixmap(IntPtr display, IntPtr drawable, uint width, uint height,
            uint depth);

        [DllImport(LibX11)]
        private static extern IntPtr XCreateGC(IntPtr display, IntPtr drawable, ulong valueMask, ref XGCValues values);

        [DllImport(LibX11)]
        private static extern int XSetForeground(IntPtr display, IntPtr gc, ulong foreground);

        [DllImport(LibX11)]
        private static extern int XFillRectangle(IntPtr display, IntPtr drawable, IntPtr gc, int x, int y, uint width,
            uint height);

        [DllImport(LibX11)]
        private static extern int XDrawPoint(IntPtr display, IntPtr drawable, IntPtr gc, int x, int y);

        [DllImport(LibX11)]
        private static extern int XFreeGC(IntPtr display, IntPtr gc);

        [DllImport(LibX11)]
        private static extern int XFreePixmap(IntPtr display, IntPtr pixmap);

        [DllImport(LibX11)]
        private static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height,
            ulong plane_mask, int format);

        [DllImport(LibX11)]
        private static extern int XDestroyImage(IntPtr xImage);

        [DllImport(LibX11)]
        private static extern ulong XGetPixel(IntPtr xImage, int x, int y);

        [DllImport(LibX11)]
        private static extern int XNextEvent(IntPtr display, ref XEvent ev);

        [DllImport(LibX11)]
        private static extern int XPending(IntPtr display);

        #endregion

        private void OnEnable()
        {
            Instance = this;
            canvas = GetComponent<Canvas>();
            Init();
            int pid = Process.GetCurrentProcess().Id;
            List<IntPtr> windows = FindWindowsByPid(pid);

            if (windows.Count > 0)
            {
                _unityWindow = windows[0]; // Typically the first is the main window
                Debug.Log($"Unity window handle: 0x{_unityWindow.ToInt64():X}");
            }
            else
            {
                ShowError("No matching windows found for PID.");
            }

            EnableClickThroughTransparency();
        }

        private void Init()
        {
            _display = XOpenDisplay(null);
            if (_display == IntPtr.Zero)
            {
                throw new Exception("Cannot open X11 display");
            }

            _rootWindow = XDefaultRootWindow(_display);
        }

        public void SetWindowPosition(float x, float y)
        {
            SetWindowPosition(new Vector2(x, y));
        }

        public void SetWindowPosition(Vector2 position)
        {
            if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
            {
                XMoveWindow(_display, _unityWindow, (int)position.x, (int)position.y);
                XFlush(_display);
            }
        }

        public Vector2 GetWindowPosition()
        {
            if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
            {
                if (XTranslateCoordinates(_display, _unityWindow, _rootWindow, 0, 0, out int absX, out int absY, out _))
                {
                    return new Vector2(absX, absY);
                }

                ShowError("XTranslateCoordinates failed.");
            }

            return Vector2.zero;
        }

        public void SetWindowSize(Vector2 size)
        {
            if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
            {
                XResizeWindow(_display, _unityWindow, (int)size.x, (int)size.y);
                XFlush(_display);
            }
        }

        public Vector2 GetWindowSize()
        {
            if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
            {
                int result = XGetWindowAttributes(_display, _unityWindow, out XWindowAttributes attributes);
                if (result != 0) // Non-zero indicates success in X11
                {
                    return new Vector2(attributes.width, attributes.height);
                }
            }

            return Vector2.zero;
        }

        public Vector2 GetMousePosition()
        {
            // Query mouse position
            int rootX = 0, rootY = 0;
            IntPtr rootWindows = XRootWindow(_display, 0);
            IntPtr rootReturn = IntPtr.Zero, childReturn = IntPtr.Zero;
            int winX = 0, winY = 0;
            uint maskReturn = 0;

            if (!XQueryPointer(_display, rootWindows, ref rootReturn, ref childReturn, ref rootX, ref rootY, ref winX,
                    ref winY, ref maskReturn))
            {
                ShowError("No mouse found.");
                return Vector2.zero;
            }

            return new Vector2(rootX, rootY);
        }

        private List<IntPtr> FindWindowsByPid(int targetPid)
        {
            var result = new List<IntPtr>();
            var atom = XInternAtom(_display, "_NET_WM_PID", false);

            if (atom == IntPtr.Zero)
            {
                Debug.Log("_NET_WM_PID atom not found");
                return result;
            }

            var windows = GetAllWindows();

            foreach (var window in windows)
            {
                int pid = GetWindowPid(window, atom);
                if (pid == targetPid)
                {
                    result.Add(window);
                }
            }

            return result;
        }

        private int GetWindowPid(IntPtr window, IntPtr pidAtom)
        {
            int status = XGetWindowProperty(_display, window, pidAtom,
                0, 1, false, (IntPtr)XaCardinal,
                out _, out _,
                out var nItems, out _, out var prop);

            if (status == 0 && prop != IntPtr.Zero && nItems > 0)
            {
                int pid = Marshal.ReadInt32(prop);
                XFree(prop);
                return pid;
            }

            return -1;
        }

        public List<IntPtr> GetAllWindows()
        {
            var result = new List<IntPtr>();
            if (XQueryTree(_display, _rootWindow, out _, out _, out var children, out var nChildren) != 0)
            {
                if (children != IntPtr.Zero && nChildren > 0)
                {
                    for (int i = 0; i < nChildren; i++)
                    {
                        IntPtr child = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                        result.Add(child);
                    }

                    XFree(children);
                }
            }

            return result;
        }

        public IntPtr GetTopLevelParent(IntPtr window)
        {
            IntPtr children;
            IntPtr current = window;

            while (XQueryTree(_display, current, out var root, out var parent, out children, out _) != 0 &&
                   parent != root && parent != IntPtr.Zero)
            {
                if (children != IntPtr.Zero)
                {
                    XFree(children);
                }

                current = parent;
            }

            if (children != IntPtr.Zero)
            {
                XFree(children);
            }

            return current;
        }

        public bool GetWindowRect(IntPtr window, out Rect rect)
        {
            rect = new Rect();
            int result = XGetWindowAttributes(_display, window, out XWindowAttributes attr);
            if (result == 0) return false;

            if (!XTranslateCoordinates(_display, window, _rootWindow, 0, 0, out int absX, out int absY, out _))
                return false;

            rect.x = absX;
            rect.y = absY;
            rect.width = absX + attr.width;
            rect.height = absY + attr.height;
            return true;
        }

        public void SetTopmost(bool topmost = true)
        {
            IntPtr wmStateAbove = XInternAtom(_display, "_NET_WM_STATE_ABOVE", true);
            if (wmStateAbove == IntPtr.Zero)
            {
                ShowError("Cannot find atom for _NET_WM_STATE_ABOVE!");
                return;
            }

            IntPtr wmNetWmState = XInternAtom(_display, "_NET_WM_STATE", true);
            if (wmNetWmState == IntPtr.Zero)
            {
                ShowError("Cannot find atom for _NET_WM_STATE!");
                return;
            }

            XClientMessageEvent xClient = new XClientMessageEvent
            {
                type = 33, // ClientMessage
                window = _unityWindow,
                message_type = wmNetWmState,
                format = 32,
                data = new IntPtr[5]
            };
            xClient.data[0] = new IntPtr(topmost ? 1 : 0); // 1=ADD, 0=REMOVE
            xClient.data[1] = wmStateAbove;
            xClient.data[2] = IntPtr.Zero;
            xClient.data[3] = IntPtr.Zero;
            xClient.data[4] = IntPtr.Zero;

            XSendEvent(_display, _rootWindow, false, 0x00100000 | 0x00080000, ref xClient);
            XFlush(_display);
        }

        public bool IsWindowVisible(IntPtr window)
        {
            int result = XGetWindowAttributes(_display, window, out XWindowAttributes attr);
            return result != 0 && attr.map_state == IsViewable;
        }

        public string GetClassName(IntPtr window)
        {
            if (XGetClassHint(_display, window, out XClassHint hint) != 0)
            {
                string cls = Marshal.PtrToStringAnsi(hint.res_class);
                XFree(hint.res_name);
                XFree(hint.res_class);
                return cls ?? "";
            }

            return "";
        }

        public IntPtr GetParent(IntPtr window)
        {
            XQueryTree(_display, window, out _, out IntPtr parent, out _, out _);
            return parent;
        }

        public IntPtr WindowFromPoint(Vector2 pt)
        {
            XTranslateCoordinates(_display, _rootWindow, _rootWindow, (int)pt.x, (int)pt.y, out _, out _,
                out IntPtr child);
            return child;
        }

        public bool IsWindowMaximized(IntPtr window)
        {
            IntPtr stateAtom = XInternAtom(_display, "_NET_WM_STATE", false);
            IntPtr maxHorz = XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_HORZ", false);
            IntPtr maxVert = XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_VERT", false);
            var states = GetWindowPropertyAtoms(window, stateAtom);
            return states.Contains(maxHorz) && states.Contains(maxVert);
        }

        public bool IsWindowFullscreen(IntPtr window)
        {
            IntPtr stateAtom = XInternAtom(_display, "_NET_WM_STATE", false);
            IntPtr fullscreen = XInternAtom(_display, "_NET_WM_STATE_FULLSCREEN", false);
            var states = GetWindowPropertyAtoms(window, stateAtom);
            if (states.Contains(fullscreen)) return true;

            // Fallback to size check
            GetWindowRect(window, out Rect r);
            int width = (int)(r.width - r.x);
            int height = (int)(r.height - r.y);
            int screenWidth = XDisplayWidth(_display, 0);
            int screenHeight = XDisplayHeight(_display, 0);
            int tolerance = 2;
            return Math.Abs(width - screenWidth) <= tolerance && Math.Abs(height - screenHeight) <= tolerance;
        }

        private List<IntPtr> GetWindowPropertyAtoms(IntPtr window, IntPtr property)
        {
            var atoms = new List<IntPtr>();
            int status = XGetWindowProperty(_display, window, property, 0, 1024, false, (IntPtr)XaAtom,
                out _, out _, out ulong nItems, out _, out IntPtr prop);

            if (status == 0 && prop != IntPtr.Zero && nItems > 0)
            {
                for (ulong i = 0; i < nItems; i++)
                {
                    IntPtr atom = Marshal.ReadIntPtr(prop, (int)(i * (ulong)IntPtr.Size));
                    atoms.Add(atom);
                }

                XFree(prop);
            }

            return atoms;
        }

        public bool IsDock(IntPtr window)
        {
            IntPtr typeAtom = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", false);
            IntPtr dock = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DOCK", false);
            var types = GetWindowPropertyAtoms(window, typeAtom);
            return types.Contains(dock);
        }

        public bool IsDesktop(IntPtr window)
        {
            IntPtr typeAtom = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", false);
            IntPtr desktop = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DESKTOP", false);
            var types = GetWindowPropertyAtoms(window, typeAtom);
            return types.Contains(desktop);
        }

        public void SetWindowInsertAfter(IntPtr w, IntPtr after)
        {
            XWindowChanges changes = new XWindowChanges
            {
                sibling = after,
                stack_mode = Below
            };
            uint mask = CwSibling | CwStackMode;
            XConfigureWindow(_display, w, mask, ref changes);
            XFlush(_display);
        }

        private void OnApplicationQuit() => Dispose();

        private void ShowError(string error) => Debug.LogError(typeof(X11Manager) + ": " + error);

        private void Dispose()
        {
            if (_display != IntPtr.Zero)
            {
#if UNITY_EDITOR_LINUX
                SetTopmost(false);
#endif
#if !UNITY_EDITOR_LINUX
            if (damage != IntPtr.Zero)
            {
                XDamageDestroy(_display, damage);
                damage = IntPtr.Zero;
            }
#endif
                XSync(_display, false);
                XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }
        }

        private void EnableClickThroughTransparency()
        {
            if (transparentInputEnabled) return;
            SetupTransparentInput();
            transparentInputEnabled = true;
            ApplyShaping();
        }

        private void SetupTransparentInput()
        {
            if (XGetWindowAttributes(_display, _unityWindow, out XWindowAttributes attrs) == 0)
            {
                ShowError("Failed to get window attributes");
                return;
            }

            if (attrs.depth != 32 || !IsArgbVisual(_display, attrs.visual))
            {
                ShowError("Unity Editor window does not have a 32-bit ARGB visual. Skipping shaping.");
                return;
            }

            XSelectInput(_display, _unityWindow, StructureNotifyMask);

            if (!XDamageQueryExtension(_display, out damageEventBase, out _))
            {
                ShowError("XDamage extension not available");
                return;
            }

            damage = XDamageCreate(_display, _unityWindow, XDamageReportNonEmpty);
            if (damage == IntPtr.Zero)
            {
                ShowError("Failed to create damage object");
                return;
            }

            IntPtr fullMask = CreateFullMask(_display, attrs.width, attrs.height);
            XShapeCombineMask(_display, _unityWindow, ShapeBounding, 0, 0, fullMask, ShapeSet);
            XFreePixmap(_display, fullMask);

            UpdateInputMask(attrs.width, attrs.height);
        }

        private bool IsArgbVisual(IntPtr display, IntPtr visual)
        {
            IntPtr formatPtr = XRenderFindVisualFormat(display, visual);
            if (formatPtr == IntPtr.Zero) return false;

            XRenderPictFormat format = Marshal.PtrToStructure<XRenderPictFormat>(formatPtr);
            return format.type == PictTypeDirect && format.direct.alphaMask != 0;
        }

        private IntPtr CreateFullMask(IntPtr display, int width, int height)
        {
            IntPtr mask = XCreatePixmap(display, XDefaultRootWindow(display), (uint)width, (uint)height, 1);

            XGCValues gcValues = default;
            gcValues.foreground = 1;
            IntPtr gc = XCreateGC(display, mask, GCForeground, ref gcValues);

            XFillRectangle(display, mask, gc, 0, 0, (uint)width, (uint)height);

            XFreeGC(display, gc);
            return mask;
        }

        private IntPtr CreateShapeMask(IntPtr display, Image image)
        {
            IntPtr mask = XCreatePixmap(display, XDefaultRootWindow(display), (uint)image.width, (uint)image.height, 1);

            XGCValues gcValues = default;
            gcValues.foreground = 0;
            gcValues.background = 0;
            IntPtr gc = XCreateGC(display, mask, GCForeground | GCBackground, ref gcValues);

            XFillRectangle(display, mask, gc, 0, 0, (uint)image.width, (uint)image.height);

            XSetForeground(display, gc, 1);
            for (int y = 0; y < image.height; y++)
            {
                for (int x = 0; x < image.width; x++)
                {
                    int idx = (y * image.width + x) * 4;
                    if (image.data[idx + 3] > 0)
                    {
                        XDrawPoint(display, mask, gc, x, y);
                    }
                }
            }

            XFreeGC(display, gc);
            return mask;
        }

        private void UpdateInputMask(int width, int height)
        {
            if (isDragging)
                return;
            IntPtr xImagePtr = XGetImage(_display, _unityWindow, 0, 0, (uint)width, (uint)height, AllPlanes, ZPixmap);
            if (xImagePtr == IntPtr.Zero)
            {
                ShowError("Failed to get image from window");
                return;
            }

            Image image = GetImageData(xImagePtr, width, height);
            XDestroyImage(xImagePtr);

            IntPtr mask = CreateShapeMask(_display, image);
            XShapeCombineMask(_display, _unityWindow, ShapeInput, 0, 0, mask, ShapeSet);
            XFreePixmap(_display, mask);
        }

        private Image GetImageData(IntPtr xImagePtr, int width, int height)
        {
            Image image;
            image.width = width;
            image.height = height;
            image.data = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    ulong pixel = XGetPixel(xImagePtr, x, y);
                    int idx = (y * width + x) * 4;
                    image.data[idx + 0] = (byte)((pixel >> 16) & 0xFF); // R
                    image.data[idx + 1] = (byte)((pixel >> 8) & 0xFF); // G
                    image.data[idx + 2] = (byte)(pixel & 0xFF); // B
                    image.data[idx + 3] = (byte)((pixel >> 24) & 0xFF); // A
                }
            }

            return image;
        }

        private async void ApplyShaping()
        {
            try
            {
                if (!transparentInputEnabled || !running || _display == IntPtr.Zero || damage == IntPtr.Zero) return;

                await Task.Run(() =>
                {
                    while (running)
                    {
                        if (_display == IntPtr.Zero)
                            break;
                        if (XPending(_display) <= 0) continue;
                        XEvent ev = default;
                        XNextEvent(_display, ref ev);

                        switch (ev.type)
                        {
                            case ConfigureNotify:
                            {
                                XConfigureEvent ce = ev.configureEvent;
                                if (ce.window == _unityWindow)
                                {
                                    int width = ce.width;
                                    int height = ce.height;

                                    IntPtr fullMask = CreateFullMask(_display, width, height);
                                    XShapeCombineMask(_display, _unityWindow, ShapeBounding, 0, 0, fullMask, ShapeSet);
                                    XFreePixmap(_display, fullMask);

                                    UpdateInputMask(width, height);
                                }

                                break;
                            }
                            case DestroyNotify:
                            {
                                XDestroyWindowEvent de = ev.destroyWindowEvent;
                                if (de.window == _unityWindow)
                                {
                                    running = false;
                                }

                                break;
                            }
                            default:
                            {
                                if (ev.type == damageEventBase)
                                {
                                    XDamageNotifyEvent de = ev.damageNotifyEvent;
                                    if (de.drawable == _unityWindow)
                                    {
                                        XDamageSubtract(_display, de.damage, IntPtr.Zero, IntPtr.Zero);

                                        XGetWindowAttributes(_display, _unityWindow, out XWindowAttributes attrs);
                                        UpdateInputMask(attrs.width, attrs.height);
                                    }
                                }

                                break;
                            }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void Update()
        {
            if (isDragging && Input.GetMouseButton(0))
            {
                Vector2 currentMousePos = GetMousePosition();
                Vector2 delta = currentMousePos - initialMousePos;
                Vector2 newPos = initialWindowPos + delta;
                SetWindowPosition(newPos);
            }
        }

        private Vector2 initialMousePos;
        private Vector2 initialWindowPos;
        private bool isDragging = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            initialMousePos = GetMousePosition();
            initialWindowPos = GetWindowPosition();
            isDragging = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isDragging = false;
        }
    }
}