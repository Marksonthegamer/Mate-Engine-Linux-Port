using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;

namespace X11
{
    // X11 Event structures

    [StructLayout(LayoutKind.Sequential)]
    public struct XClientMessageEvent
    {
        public int type;
        public IntPtr serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr message_type;
        public int format;
        public IntPtr data0;
        public IntPtr data1;
        public IntPtr data2;
        public IntPtr data3;
        public IntPtr data4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
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
    public struct XSetWindowAttributes
    {
        public IntPtr background_pixmap;
        public ulong background_pixel;
        public IntPtr border_pixmap;
        public ulong border_pixel;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public ulong backing_planes;
        public ulong backing_pixel;
        public bool save_under;
        public long event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public IntPtr colormap;
        public IntPtr cursor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XClassHint
    {
        public IntPtr res_name;
        public IntPtr res_class;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(0)] public XAnyEvent anyEvent;
        [FieldOffset(0)] public XConfigureEvent configureEvent;
        [FieldOffset(0)] public XDestroyWindowEvent destroyWindowEvent;
        [FieldOffset(0)] public XDamageNotifyEvent damageNotifyEvent;
        [FieldOffset(0)] public XClientMessageEvent clientMessageEvent;
        [FieldOffset(0)] public XSelectionEvent selectionEvent;
        [FieldOffset(0)] public XCrossingEvent crossingEvent;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct XCrossingEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public int mode;
        public int detail;
        public bool same_screen;
        public bool focus;
        public uint state;
    }
        
    [StructLayout(LayoutKind.Sequential)]
    public struct XSelectionEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr requestor;
        public IntPtr selection;
        public IntPtr target;
        public IntPtr property;
        public ulong time;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XAnyEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XConfigureEvent
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
    public struct XDestroyWindowEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr event_window;
        public IntPtr window;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XDamageNotifyEvent
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
    public struct XRectangle
    {
        public short x, y;
        public ushort width, height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XRenderPictFormat
    {
        public IntPtr id;
        public int type;
        public int depth;
        public XRenderDirectFormat direct;
        public IntPtr colormap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XRenderDirectFormat
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

    public struct Image
    {
        public byte[] Data;
        public int Width, Height;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct XImage
    {
        public int width, height;
        public int xoffset;
        public int format;
        public IntPtr data;
        public int byte_order;
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;
        public int bits_per_pixel;
    }
        
    [Serializable]
    public struct RectF
    {
        public float x1, y1, x2, y2;
    }

    public struct CoveredFractionCalculator
    {
        [BurstCompile(CompileSynchronously = true)]
        public static float Compute(NativeArray<RectF> covers, RectF target, int gridSize = 10)
        {
            int totalSamples = gridSize * gridSize;
            int coveredSamples = 0;

            for (int gy = 0; gy < gridSize; gy++)
            {
                float y = target.y1 + (target.y2 - target.y1) * (gy + 0.5f) / gridSize;
                for (int gx = 0; gx < gridSize; gx++)
                {
                    float x = target.x1 + (target.x2 - target.x1) * (gx + 0.5f) / gridSize;

                    // Check if this sample point is inside ANY cover rectangle
                    bool isCovered = false;
                    for (int j = 0; j < covers.Length; j++)
                    {
                        var c = covers[j];
                        if (x >= c.x1 && x < c.x2 && y >= c.y1 && y < c.y2)
                        {
                            isCovered = true;
                            break;  // Early exit: no need to check more
                        }
                    }

                    if (isCovered) coveredSamples++;
                }
            }

            return (float)coveredSamples / totalSamples;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XMotifWmHints
    {
        public IntPtr flags;
        public IntPtr functions;
        public IntPtr decorations;
        public IntPtr input_mode;
        public IntPtr status;
    }
        
    [StructLayout(LayoutKind.Sequential)]
    public struct XrrScreenResources
    {
        public IntPtr timestamp;
        public IntPtr configTimestamp;
        public int ncrtc;
        public IntPtr crtcs;        // IntPtr* (array of XID)
        public int noutput;
        public IntPtr outputs;      // IntPtr* (array of XID)
        public int nmode;
        public IntPtr modes;        // pointer to array of XRRScreenModeInfo
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XrrOutputInfo
    {
        public IntPtr timestamp;
        public IntPtr crtc;         // XID of current CRTC, or None
        public IntPtr name;         // pointer to null-terminated string
        public int nameLen;
        public long mm_width;       // physical width in millimeters
        public long mm_height;      // physical height in millimeters
        public Connection connection; // 0 = connected, 1 = disconnected, 2 = unknown
        public byte subpixel_order;
        public int ncrtc;
        public IntPtr crtcs;        // array of possible CRTCs
        public int nclone;
        public IntPtr clones;
        public int nmode;
        public int npreferred;
        public IntPtr modes;        // array of mode XIDs
    }
        
    public   enum Connection : byte
    {
        Connected = 0,
        Disconnected = 1,
        Unknown = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XrrCrtcInfo
    {
        public IntPtr timestamp;
        public int x, y;            // absolute position of CRTC
        public uint width, height;  // size in pixels
        public int mode;            // current mode XID
        public Rotation rotation;   // current rotation
        public int noutput;
        public IntPtr outputs;      // array of output XIDs currently driven
        public int npossible;
        public IntPtr possible;     // array of possible outputs
    }
        
    [Flags]
    public enum Rotation
    {
        Rotate0   = 1 << 0,
        Rotate90  = 1 << 1,
        Rotate180 = 1 << 2,
        Rotate270 = 1 << 3,
        ReflectX  = 1 << 4,
        ReflectY  = 1 << 5
    }

    public delegate int XErrorHandler(IntPtr display, IntPtr errorEvent);

    [StructLayout(LayoutKind.Sequential)]
    public struct XErrorEvent
    {
        public int type;
        public IntPtr display;
        public ulong resourceid;
        public ulong serial;
        public byte error_code;
        public byte request_code;
        public byte minor_code;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct XShmSegmentInfo
    {
        public IntPtr shmseg;
        public int shmid;
        private int padding;
        public IntPtr shmaddr;
        public int readOnly;
        private int padding2;
    }
}