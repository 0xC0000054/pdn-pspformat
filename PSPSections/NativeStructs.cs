////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015, 2019 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace PaintShopProFiletype
{
    internal static class NativeStructs
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
