////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    internal sealed class ColorPaletteBlock
    {
        public uint chunkSize;
        public uint entriesCount;

        public NativeStructs.RGBQUAD[] entries;

        private const uint HeaderSize = 8U;

        public ColorPaletteBlock(BinaryReader br, ushort majorVersion)
        {
            this.chunkSize = majorVersion > PSPConstants.majorVersion5 ?  br.ReadUInt32() : 0;
            this.entriesCount = br.ReadUInt32();

            this.entries = new NativeStructs.RGBQUAD[entriesCount];
            for (int i = 0; i < entriesCount; i++)
            {
                this.entries[i].rgbBlue = br.ReadByte();
                this.entries[i].rgbGreen = br.ReadByte();
                this.entries[i].rgbRed = br.ReadByte();
                this.entries[i].rgbReserved = br.ReadByte();
            } 
            
            uint dif = chunkSize - HeaderSize;
            if (dif > 0 && majorVersion > PSPConstants.majorVersion5)
            {
                br.BaseStream.Position += (long)dif;
            }
        }

    }
    
}
