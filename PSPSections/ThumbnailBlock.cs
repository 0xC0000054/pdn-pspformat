using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    /// <summary>
    /// Only present in PSP 5 (or earlier?) files
    /// </summary>
    class ThumbnailBlock
    {
        internal int width;
        internal int height;
        internal ushort bitDepth;
        internal PSPCompression compressionType; // ushort
        internal ushort planeCount;
        internal uint colorCount;
        internal uint paletteEntryCount;
        internal ushort channelCount;
        internal ChannelSubBlock[] channelBlocks;
       
        public ThumbnailBlock()
        {
        }

#if DEBUG		
        internal ColorPaletteBlock paletteBlock;

        public ThumbnailBlock(BinaryReader br)
        {
            this.width = br.ReadInt32();
            this.height = br.ReadInt32();
            this.bitDepth = br.ReadUInt16();
            this.compressionType = (PSPCompression)br.ReadUInt16();
            this.planeCount = br.ReadUInt16();
            this.colorCount = br.ReadUInt32();
            this.paletteEntryCount = br.ReadUInt32();
            this.channelCount = br.ReadUInt16();

            this.channelBlocks = new ChannelSubBlock[(int)channelCount];

            uint head = 0;
            ushort blockID = 0;
            uint initialLength = 0;
            uint totalLength = 0;
            if (this.bitDepth < 24 && this.paletteEntryCount > 0)
            {
                head = br.ReadUInt32();
                blockID = br.ReadUInt16();
                initialLength = br.ReadUInt32();
                totalLength = br.ReadUInt32();

                this.paletteBlock = new ColorPaletteBlock(br, PSPConstants.majorVersion5);
            }


            for (int i = 0; i < channelCount; i++)
            {
                head = br.ReadUInt32();
                blockID = br.ReadUInt16();
                initialLength = br.ReadUInt32();
                totalLength = br.ReadUInt32();

                ChannelSubBlock ch = new ChannelSubBlock(br, this.compressionType, PSPConstants.majorVersion5);
                this.channelBlocks[i] = ch;
            }

        } 
#endif

        public void Save(BinaryWriterEx bw)
        {
            bw.Write(PSPConstants.blockIdentifier);
            bw.Write(PSPConstants.v5ThumbnailBlock);
            bw.Write(24U);

            using (new PSPUtil.BlockLengthWriter(bw))
            {
                bw.Write(this.width);
                bw.Write(this.height);
                bw.Write(this.bitDepth);
                bw.Write((ushort)this.compressionType);
                bw.Write(this.planeCount);
                bw.Write(this.colorCount);
                bw.Write(this.paletteEntryCount);
                bw.Write(this.channelCount);

                for (int i = 0; i < channelCount; i++)
                {
                    this.channelBlocks[i].Save(bw, PSPConstants.majorVersion5);
                }
            }
        }
    }
}
