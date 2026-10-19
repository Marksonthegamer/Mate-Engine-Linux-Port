#define _GNU_SOURCE
#include <X11/Xlib.h>
#include <X11/Xatom.h>
#include <X11/extensions/shape.h>
#include <dlfcn.h>
#include <string.h>

/*
 * GNOME Wayland (Mutter) Compatibility Helper for Mate Engine Linux Port
 *
 * Provides compatibility fixes when running under XWayland on GNOME Wayland:
 * 1. Enforces borderless Motif hints (decorations = 0) even if internal drag
 *    logic attempts to re-enable them. Caches the _MOTIF_WM_HINTS atom to eliminate
 *    synchronous XGetAtomName round-trips.
 * 2. Hooks dlsym and XShapeCombineRectangles so Mono P/Invoke calls are routed
 *    through this helper.
 * 3. On ButtonPress (drag/click start), immediately expands ShapeInput to the
 *    full window so the cursor never slips out of the input region into the
 *    transparent background, preserving the XWayland pointer grab throughout
 *    multi-monitor dragging.
 * 4. While the mouse button is pressed, prevents background shaping threads from
 *    shrinking ShapeInput back to the avatar silhouette.
 * 5. Safely tracks mouse state and resets on ButtonRelease, FocusOut, or LeaveNotify,
 *    preventing click-through transparency from getting stuck.
 */

static int (*real_XChangeProperty)(Display *, Window, Atom, Atom, int, int, const unsigned char *, int) = NULL;
static void (*real_XShapeCombineRectangles)(Display *, Window, int, int, int, XRectangle *, int, int, int) = NULL;
static void (*real_XShapeCombineMask)(Display *, Window, int, int, int, Pixmap, int) = NULL;
static int (*real_XNextEvent)(Display *, XEvent *) = NULL;
static void *(*real_dlsym)(void *, const char *) = NULL;

static volatile int g_button_down = 0;
static Atom s_motif_atom = None;

static inline int is_button_down(void) {
    return __atomic_load_n(&g_button_down, __ATOMIC_ACQUIRE);
}

static inline void set_button_down(int val) {
    __atomic_store_n(&g_button_down, val, __ATOMIC_RELEASE);
}

static void ensure_real_dlsym(void) {
    if (!real_dlsym) {
        real_dlsym = (void *(*)(void *, const char *))dlvsym(RTLD_NEXT, "dlsym", "GLIBC_2.34");
        if (!real_dlsym) {
            real_dlsym = (void *(*)(void *, const char *))dlvsym(RTLD_NEXT, "dlsym", "GLIBC_2.17");
        }
        if (!real_dlsym) {
            real_dlsym = (void *(*)(void *, const char *))dlvsym(RTLD_NEXT, "dlsym", "GLIBC_2.2.5");
        }
    }
}

__attribute__((constructor))
static void init_hooks(void) {
    ensure_real_dlsym();
    if (!real_XChangeProperty && real_dlsym) {
        real_XChangeProperty = (int (*)(Display *, Window, Atom, Atom, int, int, const unsigned char *, int))
                               real_dlsym(RTLD_NEXT, "XChangeProperty");
    }
    if (!real_XShapeCombineRectangles && real_dlsym) {
        real_XShapeCombineRectangles = (void (*)(Display *, Window, int, int, int, XRectangle *, int, int, int))
                                      real_dlsym(RTLD_NEXT, "XShapeCombineRectangles");
    }
    if (!real_XShapeCombineMask && real_dlsym) {
        real_XShapeCombineMask = (void (*)(Display *, Window, int, int, int, Pixmap, int))
                                 real_dlsym(RTLD_NEXT, "XShapeCombineMask");
    }
    if (!real_XNextEvent && real_dlsym) {
        real_XNextEvent = (int (*)(Display *, XEvent *))
                          real_dlsym(RTLD_NEXT, "XNextEvent");
    }
}

int XChangeProperty(Display *disp, Window win, Atom property, Atom type,
                    int format, int mode, const unsigned char *data, int nelements) {
    if (!real_XChangeProperty) init_hooks();
    
    if (disp && s_motif_atom == None) {
        s_motif_atom = XInternAtom(disp, "_MOTIF_WM_HINTS", False);
    }
    if (s_motif_atom != None && property == s_motif_atom && data && nelements >= 3 && format == 32) {
        long hints[5] = {0};
        int count = nelements > 5 ? 5 : nelements;
        memcpy(hints, data, count * sizeof(long));
        hints[0] |= 2; // MWM_HINTS_DECORATIONS (preserve caller hints)
        hints[2] = 0;  // MWM_DECOR_NONE
        return real_XChangeProperty(disp, win, property, type, format, mode, (const unsigned char *)hints, nelements);
    }
    
    return real_XChangeProperty ? real_XChangeProperty(disp, win, property, type, format, mode, data, nelements) : 0;
}

void XShapeCombineRectangles(Display *disp, Window win, int kind,
                             int x_off, int y_off, XRectangle *rects,
                             int n_rects, int op, int ordering) {
    if (!real_XShapeCombineRectangles) init_hooks();
    
    if (kind == ShapeInput && is_button_down()) {
        // While dragging or mouse is pressed, preserve full-window input region
        // so the pointer grab is never broken when crossing outside the avatar body.
        return;
    }
    
    if (real_XShapeCombineRectangles) {
        real_XShapeCombineRectangles(disp, win, kind, x_off, y_off, rects, n_rects, op, ordering);
    }
}

int XNextEvent(Display *disp, XEvent *event_return) {
    if (!real_XNextEvent) init_hooks();
    
    int ret = real_XNextEvent ? real_XNextEvent(disp, event_return) : -1;
    if (ret == 0 && event_return) {
        if (event_return->type == ButtonPress) {
            unsigned int b = event_return->xbutton.button;
            if (b >= 1 && b <= 3) {
                set_button_down(1);
                // Instantly expand ShapeInput to full window so Wayland never drops pointer grab
                if (real_XShapeCombineMask && event_return->xbutton.window != None) {
                    real_XShapeCombineMask(disp, event_return->xbutton.window, ShapeInput, 0, 0, None, ShapeSet);
                    XFlush(disp);
                }
            }
        } else if (event_return->type == ButtonRelease) {
            unsigned int b = event_return->xbutton.button;
            if (b >= 1 && b <= 3) {
                set_button_down(0);
                if (event_return->xbutton.window != None) {
                    Window cur_focus = None;
                    int cur_revert = 0;
                    XGetInputFocus(disp, &cur_focus, &cur_revert);
                    if (cur_focus != event_return->xbutton.window) {
                        XSetInputFocus(disp, event_return->xbutton.window, RevertToParent, event_return->xbutton.time);
                    }
                }
            }
        } else if (event_return->type == FocusOut) {
            set_button_down(0);
        } else if (event_return->type == LeaveNotify) {
            if ((event_return->xcrossing.state & (Button1Mask | Button2Mask | Button3Mask | Button4Mask | Button5Mask)) == 0) {
                set_button_down(0);
            }
        }
    }
    return ret;
}

void *dlsym(void *handle, const char *symbol) {
    ensure_real_dlsym();
    
    if (strcmp(symbol, "XShapeCombineRectangles") == 0) {
        return (void *)XShapeCombineRectangles;
    }
    if (strcmp(symbol, "XChangeProperty") == 0) {
        return (void *)XChangeProperty;
    }
    if (strcmp(symbol, "XNextEvent") == 0) {
        return (void *)XNextEvent;
    }
    
    return real_dlsym ? real_dlsym(handle, symbol) : NULL;
}
