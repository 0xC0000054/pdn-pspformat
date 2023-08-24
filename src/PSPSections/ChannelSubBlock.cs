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

using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.IO;
using System.IO.Compression;

namespace PaintShopProFiletype.PSPSections
{
    internal sealed class ChannelSubBlock
    {
        public uint chunkSize;
        public uint compressedChannelLength;
        public uint uncompressedChannelLength;
        public PSPDIBType bitmapType;
        public PSPChannelType channelType;
        public byte[] channelData;

        private const uint Version6HeaderSize = 16U;
        private const uint Version5HeaderSize = 12U;

        public ChannelSubBlock(IO.BufferedBinaryReader br, PSPCompression compression, ushort majorVersion)
        {
            this.chunkSize = majorVersion > PSPConstants.majorVersion5 ? br.ReadUInt32() : 0U;
            this.compressedChannelLength = br.ReadUInt32();
            this.uncompressedChannelLength = br.ReadUInt32();
            this.bitmapType = (PSPDIBType)br.ReadUInt16();
            this.channelType = (PSPChannelType)br.ReadUInt16();
            this.channelData = null;

            if (majorVersion > PSPConstants.majorVersion5)
            {
                long bytesToSkip = (long)this.chunkSize - Version6HeaderSize;

                if (bytesToSkip > 0)
                {
                    br.Position += bytesToSkip;
                }
            }

            if (this.compressedChannelLength > 0U)
            {
                switch (compression)
                {
                    case PSPCompression.None:
                        this.channelData = br.ReadBytes((int)this.compressedChannelLength);
                        break;
                    case PSPCompression.RLE:
                        using (MemoryOwner<byte> compressedDataOwner = MemoryOwner<byte>.Allocate((int)this.compressedChannelLength))
                        {
                            Span<byte> compressedData = compressedDataOwner.Span;

                            br.ReadExactly(compressedData);
                            this.channelData = new byte[this.uncompressedChannelLength];

                            RLE.Decompress(compressedData, this.channelData);
                        }
                        break;
                    case PSPCompression.LZ77:
                        using (MemoryOwner<byte> compressedDataOwner = MemoryOwner<byte>.Allocate((int)this.compressedChannelLength))
                        {
                            br.ReadExactly(compressedDataOwner.Span);
                            this.channelData = new byte[this.uncompressedChannelLength];

                            using (Stream compressedStream = compressedDataOwner.AsStream())
                            using (ZLibStream decompressionStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
                            {
                                Span<byte> channelDataSpan = this.channelData;
                                int bytesRead = 0;

                                while ((bytesRead = decompressionStream.Read(channelDataSpan)) > 0)
                                {
                                    channelDataSpan = channelDataSpan.Slice(bytesRead);
                                }
                            }
                        }
                        break;
                }
            }
        }

        public ChannelSubBlock(ushort majorVersion, uint uncompressedSize)
        {
            this.chunkSize = majorVersion > PSPConstants.majorVersion5 ? Version6HeaderSize : Version5HeaderSize;
            this.compressedChannelLength = 0;
            this.uncompressedChannelLength = uncompressedSize;
            this.bitmapType = PSPDIBType.Image;
            this.channelType = PSPChannelType.Composite;
            this.channelData = null;
        }

        public void Save(BinaryWriter bw, ushort majorVersion)
        {
            bw.Write(PSPConstants.blockIdentifier);
            bw.Write((ushort)PSPBlockID.Channel);
            if (majorVersion > PSPConstants.majorVersion5)
            {
                bw.Write(Version6HeaderSize + this.compressedChannelLength);
                bw.Write(this.chunkSize);
            }
            else
            {
                bw.Write(Version5HeaderSize); // initial size
                bw.Write(Version5HeaderSize + this.compressedChannelLength);
            }
            bw.Write(this.compressedChannelLength);
            bw.Write(this.uncompressedChannelLength);
            bw.Write((ushort)this.bitmapType);
            bw.Write((ushort)this.channelType);

            if (this.channelData != null)
            {
                bw.Write(this.channelData);
            }
        }
    }
}
