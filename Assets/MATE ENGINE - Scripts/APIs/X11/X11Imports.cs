using System;
using System.Runtime.InteropServices;

namespace X11
{
    public static class Imports
    {

        public const string LibX11 = "libX11.so.6";
        private const string LibXExt = "libXext.so.6";
        public const string LibXRender = "libXrender.so.1";
        private const string LibXDamage = "libXdamage.so.1";
        private const string LibXRandR = "libXrandr.so.2";
        private const string LibXCursor = "libXcursor.so.1";
        private const string LibXComposite = "libXcomposite.so.1";
        private const string LibC = "libc.so.6";
        // X11 Library imports
        [DllImport(LibX11)]
        public static extern int XInitThreads();

        [DllImport(LibX11)]
        public static extern IntPtr XOpenDisplay(string displayName);

        [DllImport(LibX11)]
        public static extern void XCloseDisplay(IntPtr display);

        [DllImport(LibX11)]
        public static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(LibX11)]
        public static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

        [DllImport(LibX11)]
        public static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
            long longOffset, long longLength, bool delete, IntPtr reqType,
            out IntPtr actualTypeReturn, out int actualFormatReturn,
            out ulong nItemsReturn, out ulong bytesAfterReturn, out IntPtr propReturn);

        [DllImport(LibX11)]
        public static extern int XGetGeometry(IntPtr display, IntPtr w, out IntPtr rootReturn, out int x, out int y,
            out int width, out int height, out int borderWidth, out uint depth);



        [DllImport(LibX11)]
        public static extern int XFree(IntPtr data);

        [DllImport(LibX11)]
        public static extern int XQueryTree(IntPtr display, IntPtr window,
            out IntPtr rootReturn, out IntPtr parentReturn,
            out IntPtr childrenReturn, out uint nChildrenReturn);

        [DllImport(LibX11)]
        public static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);

        [DllImport(LibX11)]
        public static extern int XChangeWindowAttributes(IntPtr display, IntPtr window, ulong valuemask, ref XSetWindowAttributes attributes);

        [DllImport(LibX11)]
        public static extern int XMoveWindow(IntPtr display, IntPtr window, int x, int y);

        [DllImport(LibX11)]
        public static extern int XResizeWindow(IntPtr display, IntPtr window, int width, int height);

        [DllImport(LibX11)]
        public static extern bool XQueryPointer(IntPtr display, IntPtr window, ref IntPtr windowReturn,
            ref IntPtr childReturn,
            ref int rootX, ref int rootY, ref int winX, ref int winY, ref uint mask);

        [DllImport(LibX11)]
        public static extern int XQueryKeymap(IntPtr display, [In, Out] byte[] keymap);

        [DllImport(LibX11)]
        public static extern int XFlush(IntPtr display);

        [DllImport(LibX11)]
        public static extern int XScreenCount(IntPtr display);

        [DllImport(LibX11)]
        public static extern int XGetSelectionOwner(IntPtr display, IntPtr atom);

        [DllImport(LibX11)]
        public static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate,
            long eventMask, ref XClientMessageEvent eventSend);

        [DllImport(LibX11)]
        public static extern IntPtr XRootWindow(IntPtr display, int screenNumber);

        [DllImport(LibX11)]
        public static extern bool XTranslateCoordinates(IntPtr display, IntPtr srcW, IntPtr destW,
            int srcX, int srcY, out int destX, out int destY, out IntPtr child);

        [DllImport(LibX11)]
        public static extern int XGetClassHint(IntPtr display, IntPtr w, out XClassHint classHints);

        [DllImport(LibX11)]
        public static extern int XDisplayWidth(IntPtr display, int screen);

        public static int DisplayWidth(IntPtr display, int screen) => XDisplayWidth(display, screen);

        [DllImport(LibX11)]
        public static extern int XDisplayHeight(IntPtr display, int screen);

        public static int DisplayHeight(IntPtr display, int screen) => XDisplayHeight(display, screen);

        [DllImport(LibX11)]
        public static extern void XSync(IntPtr display, bool discard);

        [DllImport(LibX11)]
        public static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type,
            int format, int mode, [In, Out] IntPtr data, int nItems);

        [DllImport(LibX11)]
        public static extern int XSelectInput(IntPtr display, IntPtr window, long eventMask);

        [DllImport(LibXDamage)]
        public static extern bool XDamageQueryExtension(IntPtr display, out int eventBase, out int errorBase);

        [DllImport(LibXDamage)]
        public static extern IntPtr XDamageCreate(IntPtr display, IntPtr drawable, int level);

        [DllImport(LibXDamage)]
        public static extern void XDamageDestroy(IntPtr display, IntPtr damage);

        [DllImport(LibXDamage)]
        public static extern void XDamageSubtract(IntPtr display, IntPtr damage, IntPtr repair, IntPtr parts);

        [DllImport(LibXRender)]
        public static extern IntPtr XRenderFindVisualFormat(IntPtr display, IntPtr visual);

        [DllImport(LibX11)]
        public static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height,
            ulong planeMask, int format);

        [DllImport(LibX11)]
        public static extern int XDestroyImage(IntPtr xImage);

        [DllImport(LibX11)]
        public static extern ulong XGetPixel(IntPtr xImage, int x, int y);

        [DllImport(LibX11)]
        public static extern int XNextEvent(IntPtr display, ref XEvent ev);

        [DllImport(LibX11)]
        public static extern int XPending(IntPtr display);

        [DllImport(LibXRandR)]
        public static extern int XRRQueryExtension(IntPtr display, out IntPtr eventBase, out IntPtr errorBase);

        [DllImport(LibXRandR)]
        public static extern int XRRQueryVersion(IntPtr display, out int major, out int minor);

        [DllImport(LibXRandR)]
        public static extern IntPtr XRRGetScreenResourcesCurrent(IntPtr display, IntPtr window);

        [DllImport(LibXRandR)]
        public static extern void XRRFreeScreenResources(IntPtr resources);

        [DllImport(LibXRandR)]
        public static extern IntPtr XRRGetOutputInfo(IntPtr display, IntPtr resources, IntPtr output);

        [DllImport(LibXRandR)]
        public static extern void XRRFreeOutputInfo(IntPtr outputInfo);

        [DllImport(LibXRandR)]
        public static extern IntPtr XRRGetCrtcInfo(IntPtr display, IntPtr resources, IntPtr crtc);

        [DllImport(LibXRandR)]
        public static extern void XRRFreeCrtcInfo(IntPtr crtcInfo);

        [DllImport(LibX11)]
        public static extern string XGetAtomName(IntPtr display, IntPtr atom);

        [DllImport(LibX11)]
        public static extern bool XGetErrorText(IntPtr display, int code, byte[] buffer, int size);

        [DllImport(LibX11)]
        public static extern XErrorHandler XSetErrorHandler(XErrorHandler handler);

        [DllImport(LibX11)]
        public static extern int XQueryExtension(IntPtr display, string name, out int opcode, out int first_event, out int first_error);

        [DllImport(LibX11)]
        public static extern IntPtr XListExtensions(IntPtr display, out int nExtensions);

        [DllImport(LibX11)]
        public static extern int XSetTransientForHint(IntPtr display, IntPtr w, IntPtr propWindow);

        [DllImport(LibXExt)]
        public static extern void XShapeCombineRectangles(
            IntPtr display,
            IntPtr window,
            int destKind,
            int xOff, int yOff,
            XRectangle[] rectangles,
            int nRects,
            int op,
            int ordering
        );

        [DllImport(LibXCursor)]
        public static extern IntPtr XcursorLibraryLoadCursor(IntPtr display, string name);

        [DllImport(LibX11)]
        public static extern IntPtr XCreateFontCursor(IntPtr display, uint shape);

        [DllImport(LibX11)]
        public static extern int XDefineCursor(IntPtr display, IntPtr window, IntPtr cursor);

        [DllImport(LibX11)]
        public static extern int XFreeCursor(IntPtr display, IntPtr cursor);

        [DllImport(LibXExt)]
        public static extern bool XShmQueryExtension(IntPtr display);

        [DllImport(LibXExt)]
        public static extern int XShmAttach(IntPtr display, ref XShmSegmentInfo shminfo);

        [DllImport(LibXExt)]
        public static extern int XShmDetach(IntPtr display, ref XShmSegmentInfo shminfo);

        [DllImport(LibXExt)]
        public static extern IntPtr XShmCreateImage(IntPtr display, IntPtr visual, uint depth,
            int format, IntPtr data, ref XShmSegmentInfo shminfo, uint width, uint height);

        [DllImport(LibXExt)]
        public static extern bool XShmGetImage(IntPtr display, IntPtr drawable, IntPtr image,
            int x, int y, ulong plane_mask);

        [DllImport(LibC)]
        public static extern int shmget(uint key, IntPtr size, int shmflg);

        [DllImport(LibC)]
        public static extern IntPtr shmat(int shmid, IntPtr shmaddr, int shmflg);

        [DllImport(LibC)]
        public static extern int shmdt(IntPtr shmaddr);

        [DllImport(LibC)]
        public static extern int shmctl(int shmid, int cmd, IntPtr buf);

        [DllImport(LibXComposite)]
        public static extern void XCompositeRedirectWindow(IntPtr display, IntPtr window, int update);

        [DllImport(LibXComposite)]
        public static extern void XCompositeUnredirectWindow(IntPtr display, IntPtr window, int update);

        [DllImport(LibXComposite)]
        public static extern IntPtr XCompositeNameWindowPixmap(IntPtr display, IntPtr window);

        [DllImport(LibX11)]
        public static extern int XFreePixmap(IntPtr display, IntPtr pixmap);

        [DllImport(LibX11)]
        public static extern int XStoreName(IntPtr display, IntPtr window, string window_name);
    }
}