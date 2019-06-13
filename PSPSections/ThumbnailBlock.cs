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
    /// <summary>
    /// Only present in PSP 5 (or earlier?) files
    /// </summary>
    internal sealed class ThumbnailBlock
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

        private const uint InitalBlockLength = 24U;

        public ThumbnailBlock(int width, int height)
        {
            this.width = width;
            this.height = height;
            this.bitDepth = 24;
            this.compressionType = PSPCompression.LZ77;
            this.planeCount = 1;
            this.colorCount = (1 << 24);
            this.paletteEntryCount = 0;
            this.channelCount = 3;
        }

#if DEBUG
        internal ColorPaletteBlock paletteSubBlock;

        public ThumbnailBlock(BufferedBinaryReader br)
        {
            this.width = br.ReadInt32();
            this.height = br.ReadInt32();
            this.bitDepth = br.ReadUInt16();
            this.compressionType = (PSPCompression)br.ReadUInt16();
            this.planeCount = br.ReadUInt16();
            this.colorCount = br.ReadUInt32();
            this.paletteEntryCount = br.ReadUInt32();
            this.channelCount = br.ReadUInt16();

            this.channelBlocks = new ChannelSubBlock[this.channelCount];

            int index = 0;
            do
            {
                uint blockSig = br.ReadUInt32();
                if (blockSig != PSPConstants.blockIdentifier)
                {
                    throw new System.FormatException(Properties.Resources.InvalidBlockSignature);
                }
                PSPBlockID blockType = (PSPBlockID)br.ReadUInt16();
#pragma warning disable IDE0059 // Value assigned to symbol is never used
                uint chunkLength = br.ReadUInt32();
#pragma warning restore IDE0059 // Value assigned to symbol is never used

                switch (blockType)
                {
                    case PSPBlockID.ColorPalette:
                        this.paletteSubBlock = new ColorPaletteBlock(br, PSPConstants.majorVersion5);
                        break;
                    case PSPBlockID.Channel:
                        ChannelSubBlock block = new ChannelSubBlock(br, this.compressionType, PSPConstants.majorVersion5);
                        this.channelBlocks[index] = block;
                        index++;
                        break;
                }
            }
            while (index < this.channelCount);
        }
#endif

        public void Save(BinaryWriterEx bw)
        {
            bw.Write(PSPConstants.blockIdentifier);
            bw.Write(PSPConstants.v5ThumbnailBlock);
            bw.Write(InitalBlockLength);

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

                for (int i = 0; i < this.channelCount; i++)
                {
                    this.channelBlocks[i].Save(bw, PSPConstants.majorVersion5);
                }
            }
        }
    }
}
