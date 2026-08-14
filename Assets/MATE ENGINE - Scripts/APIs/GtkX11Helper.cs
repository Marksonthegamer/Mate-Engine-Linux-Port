using System;
using System.Runtime.InteropServices;
using Gdk;
using Debug = UnityEngine.Debug;
using Display = Gdk.Display;


public class GtkX11Helper
{
    public static GtkX11Helper Instance;
    
    // gdk_x11_window_foreign_new_for_display (gdk_display, xid) -> GdkWindow*
    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gdk_x11_window_foreign_new_for_display(IntPtr display, IntPtr windowXid);

    // gdk_x11_display_get_xdisplay (gdk_display) -> Display*
    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gdk_x11_display_get_xdisplay(IntPtr gdkDisplay);
    
    public Gtk.Window DummyParent = new("");

    private Window gdkWindow;

    public static void Init(IntPtr window)
    {
        if (Instance != null)
        {
            Debug.LogError("Trying to create multiple Gtk instances.");
            return;
        }
        Instance = new GtkX11Helper
        {
            gdkWindow = ForeignNewForDisplay(window),
            DummyParent = new Gtk.Window("")
        };
        Instance.DummyParent.Realize();
        Instance.DummyParent.SkipTaskbarHint = true;
        Instance.DummyParent.SkipPagerHint = true;
        Instance.DummyParent.Decorated = false;
        if (Instance.gdkWindow != null)
        {
            Instance.DummyParent.Window.Reparent(Instance.gdkWindow, 0, 0);
            return;
        }
        Debug.LogError("Cannot SetParent for window.");
    }

    private static Window ForeignNewForDisplay(IntPtr x11WindowXid)
    {
        if (x11WindowXid == IntPtr.Zero)
            throw new ArgumentException("XID cannot be zero");
        
        IntPtr foreign = gdk_x11_window_foreign_new_for_display(Display.Default.Handle, x11WindowXid);
        if (foreign != IntPtr.Zero) return new Window(foreign);
        Debug.LogError($"Failed to create foreign GdkWindow for XID 0x{x11WindowXid:X}.");
        return null;
    }
}
