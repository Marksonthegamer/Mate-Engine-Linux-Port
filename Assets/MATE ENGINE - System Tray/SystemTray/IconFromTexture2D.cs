using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Utils
{
    public static partial class TrayIcon
    {
        private const uint BI_RGB = 0x0000;
        private const uint DIB_RGB_COLORS = 0;

        private static IntPtr CreateHIconFromTexture2D(ref Texture2D texture)
        {
            int width = texture.width;
            int height = texture.height;
            byte[] bgraPixels = new byte[width * height * 4];

            GCHandle pinnedPixels = GCHandle.Alloc(bgraPixels, GCHandleType.Pinned);
            IntPtr pixelPtr = pinnedPixels.AddrOfPinnedObject();

            try
            {
                Color32[] pixels = texture.GetPixels32();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color32 p = pixels[y * width + x];
                        int destIndex = (y * width + x) * 4;
                        bgraPixels[destIndex + 0] = p.b;
                        bgraPixels[destIndex + 1] = p.g;
                        bgraPixels[destIndex + 2] = p.r;
                        bgraPixels[destIndex + 3] = p.a;
                    }
                }

                BITMAPINFO bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = height;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = BI_RGB;

                IntPtr ppvBits;
                IntPtr hbmColor = WinAPI.CreateDIBSection(IntPtr.Zero, ref bmi, DIB_RGB_COLORS, out ppvBits, IntPtr.Zero, 0);
                if (hbmColor == IntPtr.Zero)
                    throw new SystemException($"CreateDIBSection Failed. Error: {Marshal.GetLastWin32Error()}");
                else
                    Marshal.Copy(bgraPixels, 0, ppvBits, bgraPixels.Length);

                IntPtr hbmMask = WinAPI.CreateBitmap(width, height, 1, 1, IntPtr.Zero);
                if (hbmMask == IntPtr.Zero)
                {
                    WinAPI.DeleteObject(hbmColor);
                    throw new SystemException($"CreateBitmap Failed. Error: {Marshal.GetLastWin32Error()}");
                }

                ICONINFO iconInfo = new ICONINFO();
                iconInfo.fIcon = true;
                iconInfo.hbmMask = hbmMask;
                iconInfo.hbmColor = hbmColor;

                IntPtr hIcon = WinAPI.CreateIconIndirect(ref iconInfo);
                if (hIcon == IntPtr.Zero)
                    throw new SystemException($"CreateIconIndirect Failed. Error: {Marshal.GetLastWin32Error()}");

                WinAPI.DeleteObject(hbmColor);
                WinAPI.DeleteObject(hbmMask);
                return hIcon;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception in CreateHIconFromTexture2D:\n{e}");
                return IntPtr.Zero;
            }
            finally
            {
                if (pinnedPixels.IsAllocated)
                    pinnedPixels.Free();
            }
        }
    }
}
