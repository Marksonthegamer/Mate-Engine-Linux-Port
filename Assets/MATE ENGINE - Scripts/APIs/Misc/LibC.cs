using System;
using System.Runtime.InteropServices;

namespace Misc
{
    public class LibC
    {
        [DllImport("libc")]
        public static extern IntPtr setenv(string name, string value, int overwrite);
    }
}