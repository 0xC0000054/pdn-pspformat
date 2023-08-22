////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015, 2019, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace PaintShopProFiletype.PSPSections
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct RGBQUAD
    {
        public readonly byte red;
        public readonly byte green;
        public readonly byte blue;
        public readonly byte reserved;

        public RGBQUAD(IO.BufferedBinaryReader reader)
        {
            this.red = reader.ReadByte();
            this.green = reader.ReadByte();
            this.blue = reader.ReadByte();
            this.reserved = reader.ReadByte();
        }
    }
}
