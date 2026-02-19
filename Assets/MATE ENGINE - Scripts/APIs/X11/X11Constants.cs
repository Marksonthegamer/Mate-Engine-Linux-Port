namespace X11
{
    public class Constants
    {
        // Constants
        public const int XaCardinal = 6;
        public const int XaAtom = 4;
        public const int IsViewable = 2;
        public const int XaWindow = 33;
        
        public const long MwmHintsFlags = 1L << 1; // Use decorations
        public const long MwmDecorationsNone = 0; // No decorations
        public const int PropModeReplace = 0;

        public const int ClientMessage = 33;
        public const long StructureNotifyMask = (1L << 17);
        public const long SubstructureRedirectMask = 0x00080000;
        public const long SubstructureNotifyMask = 0x00040000;
        public const long EnterWindowMask = (1L << 4);
        public const long LeaveWindowMask = (1L << 5);
        public const long PropertyChangeMask = (1L << 22);
        public const int ConfigureNotify = 22;
        public const int DestroyNotify = 17;
        public const int PropertyNotify = 28;
        public const int ShapeBounding = 0;
        public const int ShapeInput = 2;
        public const int ShapeSet = 0;
        public const int PictTypeDirect = 1;
        public const int XDamageReportNonEmpty = 3;
        public const int ZPixmap = 2;
        public const ulong AllPlanes = 0xFFFFFFFFFFFFFFFFUL; // For 64-bit
        public const int Unsorted = 0;
        public const int YSorted = 1;
        public const uint XC_LEFT_PTR = 68;
        public const uint XC_HAND2 = 60;
        public const int EnterNotify = 7;
        public const int LeaveNotify = 8;
        public const int CompositeRedirectAutomatic = 0;

        public const int IPC_RMID = 0;
        public const int IPC_PRIVATE = 0;
        public const int IPC_CREAT = 00001000;
    }
}