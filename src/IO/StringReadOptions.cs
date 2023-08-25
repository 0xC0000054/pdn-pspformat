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

namespace PaintShopProFiletype.IO
{
    internal enum StringReadOptions
    {
        None = 0,
        TrimWhiteSpace = (1 << 0),
        TrimNullTerminator = (1 << 1),
    }
}
