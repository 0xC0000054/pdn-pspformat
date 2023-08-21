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

namespace PaintShopProFiletype
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RGBQUAD
    {
        public byte red;
        public byte green;
        public byte blue;
        public byte reserved;
    }
}
