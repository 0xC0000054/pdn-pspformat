using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PaintShopProFiletype
{
    static class NativeStructs
    {
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct RGBQUAD
        {

            /// BYTE->unsigned char
            public byte rgbBlue;

            /// BYTE->unsigned char
            public byte rgbGreen;

            /// BYTE->unsigned char
            public byte rgbRed;

            /// BYTE->unsigned char
            public byte rgbReserved;
        }

    }
}
