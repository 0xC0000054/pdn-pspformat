using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    struct ColorPaletteBlock
    {
        public uint chunkSize;
        public uint entriesCount;

        public NativeStructs.RGBQUAD[] entries;

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
            
            uint dif = chunkSize - 8U;
            if (dif > 0 && majorVersion > PSPConstants.majorVersion5)
            {
                br.BaseStream.Position += (long)dif;
            }
        }

    }
    
}
