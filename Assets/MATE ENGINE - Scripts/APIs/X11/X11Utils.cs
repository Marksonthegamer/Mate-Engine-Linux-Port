using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace X11
{
    public static class X11Utils
    {
        private static Dictionary<int, string> _x11ExtensionsMap = new();

        public static void RegisterExtension(IntPtr display)
        {
            IntPtr listPtr = Imports.XListExtensions(display, out int count);

            if (listPtr == IntPtr.Zero || count == 0) return;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    IntPtr strPtr = Marshal.ReadIntPtr(listPtr, i * IntPtr.Size);

                    string name = Marshal.PtrToStringAnsi(strPtr);

                    if (!string.IsNullOrEmpty(name))
                    {
                        if (Imports.XQueryExtension(display, name, out int opcode, out _, out _) == 0)
                        {
                            var error = $"Cannot query extension {name}";
                            Console.WriteLine($"\u001b[31m{nameof(X11Utils)}: {error}\u001b[0m");
                            continue;
                        }
                        if (opcode > 0)
                        {
                            _x11ExtensionsMap[opcode] = name;
                        }
                    }
                }
            }
            finally
            {
                Imports.XFree(listPtr);
            }
        }

        public static string GetRequestName(byte requestCode)
        {
            int code = requestCode;

            if (_x11ExtensionsMap.TryGetValue(code, out string extName))
            {
                return $"[{extName}]";
            }

            return requestCode switch
            {
                1 => "X_CreateWindow",
                2 => "X_ChangeWindowAttributes",
                3 => "X_GetWindowAttributes",
                4 => "X_DestroyWindow",
                5 => "X_DestroySubwindows",
                6 => "X_ChangeSaveSet",
                7 => "X_ReparentWindow",
                8 => "X_MapWindow",
                9 => "X_MapSubwindows",
                10 => "X_UnmapWindow",
                11 => "X_UnmapSubwindows",
                12 => "X_ConfigureWindow",
                13 => "X_CirculateWindow",
                14 => "X_GetGeometry",
                15 => "X_QueryTree",
                16 => "X_InternAtom",
                17 => "X_GetAtomName",
                18 => "X_ChangeProperty",
                19 => "X_DeleteProperty",
                20 => "X_GetProperty",
                21 => "X_ListProperties",
                22 => "X_SetSelectionOwner",
                23 => "X_GetSelectionOwner",
                24 => "X_ConvertSelection",
                25 => "X_SendEvent",
                26 => "X_GrabPointer",
                27 => "X_UngrabPointer",
                28 => "X_GrabButton",
                29 => "X_UngrabButton",
                30 => "X_ChangeActivePointerGrab",
                31 => "X_GrabKeyboard",
                32 => "X_UngrabKeyboard",
                33 => "X_GrabKey",
                34 => "X_UngrabKey",
                35 => "X_AllowEvents",
                36 => "X_GrabServer",
                37 => "X_UngrabServer",
                38 => "X_QueryPointer",
                39 => "X_GetMotionEvents",
                40 => "X_TranslateCoords",
                41 => "X_WarpPointer",
                42 => "X_SetInputFocus",
                43 => "X_GetInputFocus",
                44 => "X_QueryKeymap",
                45 => "X_OpenFont",
                46 => "X_CloseFont",
                47 => "X_QueryFont",
                48 => "X_QueryTextExtents",
                49 => "X_ListFonts",
                50 => "X_ListFontsWithInfo",
                51 => "X_SetFontPath",
                52 => "X_GetFontPath",
                53 => "X_CreatePixmap",
                54 => "X_FreePixmap",
                55 => "X_CreateGC",
                56 => "X_ChangeGC",
                57 => "X_CopyGC",
                58 => "X_SetDashes",
                59 => "X_SetClipRectangles",
                60 => "X_FreeGC",
                61 => "X_ClearArea",
                62 => "X_CopyArea",
                63 => "X_CopyPlane",
                64 => "X_PolyPoint",
                65 => "X_PolyLine",
                66 => "X_PolySegment",
                67 => "X_PolyRectangle",
                68 => "X_PolyArc",
                69 => "X_FillPoly",
                70 => "X_PolyFillRectangle",
                71 => "X_PolyFillArc",
                72 => "X_PutImage",
                73 => "X_GetImage",
                74 => "X_PolyText8",
                75 => "X_PolyText16",
                76 => "X_ImageText8",
                77 => "X_ImageText16",
                78 => "X_CreateColormap",
                79 => "X_FreeColormap",
                80 => "X_CopyColormapAndFree",
                81 => "X_InstallColormap",
                82 => "X_UninstallColormap",
                83 => "X_ListInstalledColormaps",
                84 => "X_AllocColor",
                85 => "X_AllocNamedColor",
                86 => "X_AllocColorCells",
                87 => "X_AllocColorPlanes",
                88 => "X_FreeColors",
                89 => "X_StoreColors",
                90 => "X_StoreNamedColor",
                91 => "X_QueryColors",
                92 => "X_LookupColor",
                93 => "X_CreateCursor",
                94 => "X_CreateGlyphCursor",
                95 => "X_FreeCursor",
                96 => "X_RecolorCursor",
                97 => "X_QueryBestSize",
                98 => "X_QueryExtension",
                99 => "X_ListExtensions",
                100 => "X_ChangeKeyboardMapping",
                101 => "X_GetKeyboardMapping",
                102 => "X_ChangeKeyboardControl",
                103 => "X_GetKeyboardControl",
                104 => "X_Bell",
                105 => "X_ChangePointerControl",
                106 => "X_GetPointerControl",
                107 => "X_SetScreenSaver",
                108 => "X_GetScreenSaver",
                109 => "X_ChangeHosts",
                110 => "X_ListHosts",
                111 => "X_SetAccessControl",
                112 => "X_SetCloseDownMode",
                113 => "X_KillClient",
                114 => "X_RotateProperties",
                115 => "X_ForceScreenSaver",
                116 => "X_SetPointerMapping",
                117 => "X_GetPointerMapping",
                118 => "X_SetModifierMapping",
                119 => "X_GetModifierMapping",
                127 => "X_NoOperation",
                _ => $"Request_{requestCode}"
            };
        }
    }
}