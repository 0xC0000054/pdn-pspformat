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

using System;

namespace PaintShopProFiletype.PSPSections
{
    internal static class RLE
    {
        public static void Decompress(ReadOnlySpan<byte> compressedData, Span<byte> uncompressedData)
        {
            int srcIndx = 0;
            int dstIndx = 0;

            int len;
            byte value;
            int compressedLength = compressedData.Length;
            int uncompressedLength = uncompressedData.Length;

            while (srcIndx < compressedLength && dstIndx < uncompressedLength)
            {
                len = compressedData[srcIndx++];

                if (len > 128)
                {
                    len -= 128;
                    value = compressedData[srcIndx++];

                    uncompressedData.Slice(dstIndx, len).Fill(value);
                }
                else
                {
                    compressedData.Slice(srcIndx, len).CopyTo(uncompressedData.Slice(dstIndx, len));
                }

                dstIndx += len;
            }
        }
    }
}
