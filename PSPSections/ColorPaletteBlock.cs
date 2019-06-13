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

namespace PaintShopProFiletype.PSPSections
{
    internal sealed class ColorPaletteBlock
    {
        public uint chunkSize;
        public uint entriesCount;

        public NativeStructs.RGBQUAD[] entries;

        private const uint HeaderSize = 8U;

        public ColorPaletteBlock(BufferedBinaryReader br, ushort majorVersion)
        {
            this.chunkSize = majorVersion > PSPConstants.majorVersion5 ?  br.ReadUInt32() : 0;
            this.entriesCount = br.ReadUInt32();

            this.entries = new NativeStructs.RGBQUAD[this.entriesCount];
            for (int i = 0; i < this.entriesCount; i++)
            {
                this.entries[i].rgbBlue = br.ReadByte();
                this.entries[i].rgbGreen = br.ReadByte();
                this.entries[i].rgbRed = br.ReadByte();
                this.entries[i].rgbReserved = br.ReadByte();
            }

            long dif = (long)this.chunkSize - HeaderSize;
            if (dif > 0 && majorVersion > PSPConstants.majorVersion5)
            {
                br.Position += dif;
            }
        }
    }

}
