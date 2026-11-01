using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Gtk;
using NativeLibraryLoader;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class EarlyEnvSet
{
    [DllImport(WindowManager.LibX11)]
    private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out WindowManager.XWindowAttributes attributes);
    
    [DllImport(WindowManager.LibXRender)]
    private static extern IntPtr XRenderFindVisualFormat(IntPtr display, IntPtr visual);
    
    [DllImport(WindowManager.LibX11)]
    private static extern IntPtr XOpenDisplay(string displayName);
    
    [DllImport(WindowManager.LibX11)]
    private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);
    
    [DllImport(WindowManager.LibX11)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);
    
    [DllImport(WindowManager.LibX11)]
    private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
        long longOffset, long longLength, bool delete, IntPtr reqType,
        out IntPtr actualTypeReturn, out int actualFormatReturn,
        out ulong nItemsReturn, out ulong bytesAfterReturn, out IntPtr propReturn);
    
    [DllImport(WindowManager.LibX11)]
    private static extern int XGetSelectionOwner(IntPtr display, IntPtr atom);
    
    [DllImport(WindowManager.LibX11)]
    private static extern int XScreenCount(IntPtr display);
    
    [DllImport(WindowManager.LibX11)]
    private static extern int XFree(IntPtr data);
    
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitBeforeAnything()
    {
        #if UNITY_EDITOR
        return;
        #endif
        string[] candidates = {"libX11.so.6", "libXext.so.6", "libXrender.so.1", "libXdamage.so.1", "libXrandr.so.2", "libXcursor.so.1", "libXcomposite.so.1", "libpulse.so.0", "libgdk-3.so.0", "libgtk-3.so.0", "libayatana-appindicator3.so.1"};
        List<string> missing = new();
        foreach (var name in candidates)
        {
            var libLoader = LibraryLoader.GetPlatformDefaultLoader();
            try
            {
                var ptr = libLoader.LoadNativeLibrary(name);
                libLoader.FreeNativeLibrary(ptr);
            }
            catch
            {
                Debug.LogError($"Cannot load library {name}. Consider checking if its installed correctly.");
                missing.Add(name);
            }
        }
        string[] argc = { };
        if (!Gtk.Application.InitCheck(string.Empty, ref argc))
        {
            throw new Exception("Gtk initialization failed.");
        }
        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            throw new Exception("Cannot open X11 display");
        }
        if (!CheckEnvAndVisual(display, missing))
        {
            UnityEngine.Application.Quit(11);
            return;
        }
        if (!IsCompositionSupported(display))
        {
            var dialog = new MessageDialog(GtkX11Helper.Instance.DummyParent, DialogFlags.DestroyWithParent, MessageType.Warning, ButtonsType.Ok, false, "Composition is unavailable for this window manager.");
            dialog.SecondaryText = "A compositor is required for MateEngine to show a transparent background.\n\nIf you are running MateEngine on WMs that don't compose (like Openbox), try installing a compositing manager (such as picom) and configure it correctly, or simply switch to Mutter (GNOME), Xfwm4 (Xfce4) and other WMs which natively supports composition.\n\nIf you are running KWin (KDE), please make sure \"Allow applications to block compositing\" is turned off in KDE System Settings (It's in the compositor section of Display and Monitor).";
            dialog.MessageType = MessageType.Warning;
            var image = new Image(new Gdk.Pixbuf(Resources.Load<Texture2D>("KWinHint").EncodeToPNG()));
            image.Halign = Align.Center;
            image.Show();
            dialog.ContentArea?.PackStart(image, false, false, 0);
            dialog.ShowAll();
            dialog.Response += (_, _) =>
            {
                dialog.Hide();
                Gtk.Application.Quit();
            };
            Gtk.Application.Run();
        }
    }
    
    private static bool IsCompositionSupported(IntPtr display)
    {
        for (int screen = 0; screen < XScreenCount(display); screen++)
        {
            IntPtr selectionAtom = XInternAtom(display, "_NET_WM_CM_S" + screen, false);
            if (selectionAtom == IntPtr.Zero)
                continue;
            return XGetSelectionOwner(display, selectionAtom) != 0;
        }
        return false;
    }

    static bool CheckEnvAndVisual(IntPtr display, List<string> missingLibs)
    {
        int pid = Process.GetCurrentProcess().Id;
        List<IntPtr> windows = FindWindowsByPid(display, pid);
        
        IntPtr unityWindow = IntPtr.Zero;

        if (windows.Count > 0)
        {
            unityWindow = windows[0]; // Typically the first is the main window
        }
        GtkX11Helper.Init(unityWindow);
        if (XGetWindowAttributes(display, unityWindow, out WindowManager.XWindowAttributes attrs) == 0)
        {
            Debug.LogError("Failed to get window attributes");
            return false;
        }
        if (missingLibs.Count > 0)
        {
            var dialog = new MessageDialog(GtkX11Helper.Instance.DummyParent, DialogFlags.DestroyWithParent, MessageType.Warning, ButtonsType.Ok, false, "Some required native libraries could not be loaded.");
            dialog.SecondaryText = "This app relies on these dependencies to run.";
            dialog.MessageType = MessageType.Warning;
            var textView = new TextView();
            textView.Editable = false;
            textView.CursorVisible = false;
            textView.AcceptsTab = false;
            textView.WrapMode = Gtk.WrapMode.Word;
            
            var buffer = textView.Buffer;
        
            buffer.Text = missingLibs.Count == 0 ? "How do you get here?" : string.Join("\n", missingLibs);
            
            dialog.ContentArea.PackStart(textView, true, true, 12);
            
            dialog.Response += (_, _) =>
            {
                dialog.Hide();
                Gtk.Application.Quit();
            };
            dialog.DestroyEvent += (_, _) =>
            {
                dialog.Hide();
                Gtk.Application.Quit();
            };
            dialog.ShowAll();
            Gtk.Application.Run();
            return false;
        }
        
        if (attrs.depth != 32 || !IsArgbVisual(display, attrs.visual))
        {
            var dialog = new MessageDialog(GtkX11Helper.Instance.DummyParent, DialogFlags.DestroyWithParent, MessageType.Warning, ButtonsType.Ok, false, "It looks like MateEngine is not running under an ARGB visual.");
            dialog.SecondaryText = "MateEngine must rely on a true transparent canvas (aka ARGB visual) to show a transparent background. Without it, your avatar will display on a big black background.\n\nTo acquire an ARGB visual, try launching MateEngine using the launch script (usually called launch.sh) provided in MateEngine executable path.";
            dialog.MessageType = MessageType.Warning;
            dialog.Show();
            dialog.Response += (_, _) =>
            {
                dialog.Hide();
                Gtk.Application.Quit();
            };
            Gtk.Application.Run();
            return false;
        }

        return true;
    }
    
    private static bool IsArgbVisual(IntPtr display, IntPtr visual)
    {
        IntPtr formatPtr = XRenderFindVisualFormat(display, visual);
        if (formatPtr == IntPtr.Zero) return false;

        WindowManager.XRenderPictFormat format = Marshal.PtrToStructure<WindowManager.XRenderPictFormat>(formatPtr);
        return format.type == 1 && format.direct.alphaMask != 0;
    }
    
    private static List<IntPtr> FindWindowsByPid(IntPtr display, int targetPid)
    {
        var result = new List<IntPtr>();
        var atom = XInternAtom(display, "_NET_WM_PID", false);

        if (atom == IntPtr.Zero)
        {
            Debug.Log("_NET_WM_PID atom not found");
            return result;
        }

        var windows = GetAllVisibleWindows(display, XDefaultRootWindow(display));

        foreach (var window in windows)
        {
            int pid = GetWindowPid(display, window, atom);
            if (pid == targetPid)
            {
                result.Add(window);
            }
        }

        return result;
    }
    
    private static int GetWindowPid(IntPtr display, IntPtr window, IntPtr pidAtom)
    {
        int status = XGetWindowProperty(display, window, pidAtom,
            0, 1, false, (IntPtr)6,
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
    
    private static List<IntPtr> GetAllVisibleWindows(IntPtr display, IntPtr rootWindow)
    {
        var result = new List<IntPtr>();

        IntPtr clientListAtom = XInternAtom(display, "_NET_CLIENT_LIST", true);
        if (clientListAtom != IntPtr.Zero)
        {
            int status = XGetWindowProperty(display, rootWindow, clientListAtom, 0, 1024, false, (IntPtr)33,
                out IntPtr actualType, out int actualFormat, out ulong nItems, out _, out IntPtr prop);

            if (status == 0 && actualType == (IntPtr)33 && actualFormat == 32 && prop != IntPtr.Zero)
            {
                for (ulong i = 0; i < nItems; i++)
                {
                    IntPtr win = Marshal.ReadIntPtr(prop, (int)(i * (ulong)IntPtr.Size));
                    result.Add(win);
                }
                XFree(prop);
                return result;
            }
            if (prop != IntPtr.Zero) XFree(prop);
        }
        return result;
    }
}
