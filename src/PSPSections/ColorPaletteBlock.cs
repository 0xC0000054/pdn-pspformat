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

namespace PaintShopProFiletype.PSPSections
{
    internal sealed class ColorPaletteBlock
    {
        public uint chunkSize;
        public uint entriesCount;

        public RGBQUAD[] entries;

        private const uint HeaderSize = 8U;

        public ColorPaletteBlock(BufferedBinaryReader br, ushort majorVersion)
        {
            this.chunkSize = majorVersion > PSPConstants.majorVersion5 ?  br.ReadUInt32() : 0;
            this.entriesCount = br.ReadUInt32();

            this.entries = new RGBQUAD[this.entriesCount];
            for (int i = 0; i < this.entriesCount; i++)
            {
                this.entries[i].red = br.ReadByte();
                this.entries[i].green = br.ReadByte();
                this.entries[i].blue = br.ReadByte();
                this.entries[i].reserved = br.ReadByte();
            }

            if (majorVersion > PSPConstants.majorVersion5)
            {
                long bytesToSkip = (long)this.chunkSize - HeaderSize;

                if (bytesToSkip > 0)
                {
                    br.Position += bytesToSkip;
                }
            }
        }
    }

}
