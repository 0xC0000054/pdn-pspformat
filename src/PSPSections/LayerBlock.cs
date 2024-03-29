﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015, 2019, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintShopProFiletype.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PaintShopProFiletype.PSPSections
{
    internal readonly struct BlendRange
    {
        public readonly uint sourceRange;
        public readonly uint destRange;

        public BlendRange(EndianBinaryReader reader)
        {
            this.sourceRange = reader.ReadUInt32();
            this.destRange = reader.ReadUInt32();
        }

        public BlendRange(uint sourceRange, uint destRange)
        {
            this.sourceRange = sourceRange;
            this.destRange = destRange;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(this.sourceRange);
            writer.Write(this.destRange);
        }
    }

    internal sealed class LayerBitmapInfoChunk
    {
        public uint chunkSize;
        public ushort bitmapCount;
        public ushort channelCount;
        public ChannelSubBlock[] channels;

        internal const uint HeaderSize = 8U;

        public LayerBitmapInfoChunk(EndianBinaryReader br, PSPCompression compression, ushort majorVersion)
        {
            long startOffset = br.Position;

            this.chunkSize = br.ReadUInt32();
            this.bitmapCount = br.ReadUInt16();
            this.channelCount = br.ReadUInt16();
            this.channels = new ChannelSubBlock[this.channelCount];

            long bytesToSkip = this.chunkSize - (br.Position - startOffset);
            if (bytesToSkip > 0L)
            {
                br.Position += bytesToSkip;
            }

            for (int i = 0; i < this.channelCount; i++)
            {
                uint head = br.ReadUInt32();
                if (head != PSPConstants.blockIdentifier)
                {
                    throw new FormatException(Properties.Resources.InvalidBlockSignature);
                }
                ushort blockID = br.ReadUInt16();
                PSPUtil.CheckBlockType(blockID, PSPBlockID.Channel);
#pragma warning disable IDE0059 // Value assigned to symbol is never used
                uint size = br.ReadUInt32();
#pragma warning restore IDE0059 // Value assigned to symbol is never used

                this.channels[i] = new ChannelSubBlock(br, compression, majorVersion);
            }
        }

        public LayerBitmapInfoChunk(EndianBinaryReader br, PSPCompression compression, ushort v5BitmapCount, ushort v5ChannelCount)
        {
            this.chunkSize = 0;
            this.bitmapCount = v5BitmapCount;
            this.channelCount = v5ChannelCount;
            this.channels = new ChannelSubBlock[this.channelCount];

            for (int i = 0; i < this.channelCount; i++)
            {
                uint head = br.ReadUInt32();
                if (head != PSPConstants.blockIdentifier)
                {
                    throw new FormatException(Properties.Resources.InvalidBlockSignature);
                }
                ushort blockID = br.ReadUInt16();
                PSPUtil.CheckBlockType(blockID, PSPBlockID.Channel);
#pragma warning disable IDE0059 // Value assigned to symbol is never used
                uint initialSize = br.ReadUInt32();
                uint size = br.ReadUInt32();
#pragma warning restore IDE0059 // Value assigned to symbol is never used

                this.channels[i] = new ChannelSubBlock(br, compression, PSPConstants.majorVersion5);
            }
        }

        public LayerBitmapInfoChunk(int bitmapCount, ChannelSubBlock[] channels)
        {
            this.chunkSize = HeaderSize;
            this.bitmapCount = (ushort)bitmapCount;
            this.channelCount = checked((ushort)channels.Length);
            this.channels = channels;
        }

        public void Save(BinaryWriter bw, ushort majorVersion)
        {
            if (majorVersion > PSPConstants.majorVersion5)
            {
                bw.Write(this.chunkSize);
                bw.Write(this.bitmapCount);
                bw.Write(this.channelCount);
            }

            for (int i = 0; i < this.channelCount; i++)
            {
                this.channels![i].Save(bw, majorVersion);
            }
        }
    }

    internal sealed class LayerInfoChunk
    {
        internal const uint Version5ChunkLength = 375U;
        /// <summary>
        /// The base chunk size of the version 6 header, 119 bytes + 2 for the name length.
        /// </summary>
        private const uint Version6BaseChunkSize = 119 + sizeof(ushort);
        private const int BlendRangeCount = 5;

        public uint chunkSize;
        public string name;
        public PSPLayerType type;
        public Rectangle imageRect; // Int32
        public Rectangle saveRect; // Int32
        public byte opacity;
        public PSPBlendModes blendMode; // byte
        public PSPLayerProperties layerFlags;
        public byte protectedTransparency;
        public byte linkGroup;
        public Rectangle maskRect;
        public Rectangle saveMaskRect;
        public byte maskLinked;
        public byte maskDisabled;
        public byte invertMaskOnBlend;
        public ushort blendRangeCount;
        public BlendRange[] blendRanges;

        public ushort v5BitmapCount;
        public ushort v5ChannelCount;

        public byte useHighlightColor;
        public uint highlightColor;

        public LayerInfoChunk(EndianBinaryReader br, ushort majorVersion)
        {
            long startOffset = br.Position;

            if (majorVersion > PSPConstants.majorVersion5)
            {
                this.chunkSize = br.ReadUInt32();
                ushort nameLen = br.ReadUInt16();
                this.name = br.ReadAsciiString(nameLen, StringReadOptions.None);
            }
            else
            {
                this.chunkSize = 0;
                this.name = br.ReadAsciiString(256, StringReadOptions.TrimNullTerminator);
            }

            this.type = (PSPLayerType)br.ReadByte();

            if (majorVersion <= PSPConstants.majorVersion5 && this.type == PSPLayerType.Undefined)
            {
                this.type = PSPLayerType.Raster;
            }

            this.imageRect = Rectangle.FromLTRB(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
            this.saveRect = Rectangle.FromLTRB(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
            this.opacity = br.ReadByte();
            this.blendMode = (PSPBlendModes)br.ReadByte();
            this.layerFlags = (PSPLayerProperties)br.ReadByte();
            this.protectedTransparency = br.ReadByte();
            this.linkGroup = br.ReadByte();
            this.maskRect = Rectangle.FromLTRB(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
            this.saveMaskRect = Rectangle.FromLTRB(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
            this.maskLinked = br.ReadByte();
            this.maskDisabled = br.ReadByte();
            this.invertMaskOnBlend = br.ReadByte();
            this.blendRangeCount = br.ReadUInt16();
            this.blendRanges = new BlendRange[BlendRangeCount];
            for (int i = 0; i < this.blendRanges.Length; i++)
            {
                this.blendRanges[i] = new BlendRange(br);
            }
            this.v5BitmapCount = 0;
            this.v5ChannelCount = 0;
            this.useHighlightColor = 0;
            this.highlightColor = 0;
            if (majorVersion >= PSPConstants.majorVersion8)
            {
                this.useHighlightColor = br.ReadByte();
                this.highlightColor = br.ReadUInt32();
            }
            else if (majorVersion <= PSPConstants.majorVersion5)
            {
                this.v5BitmapCount = br.ReadUInt16();
                this.v5ChannelCount = br.ReadUInt16();
            }

            if (majorVersion > PSPConstants.majorVersion5)
            {
                long bytesToSkip = this.chunkSize - (br.Position - startOffset);
                if (bytesToSkip > 0)
                {
                    br.Position += bytesToSkip;
                }
            }
        }

        public LayerInfoChunk(PaintDotNet.Layer layer, PSPBlendModes blendMode, Rectangle savedBounds, ushort majorVersion)
        {
            if (majorVersion > PSPConstants.majorVersion5)
            {
                this.chunkSize = Version6BaseChunkSize + (uint)Encoding.ASCII.GetByteCount(layer.Name);
            }
            else
            {
                this.chunkSize = 0U;
            }
            this.name = layer.Name;
            this.type = majorVersion > PSPConstants.majorVersion5 ? PSPLayerType.Raster : 0;
            this.imageRect = layer.Bounds;
            this.saveRect = savedBounds;
            this.opacity = layer.Opacity;
            this.blendMode = blendMode;
            this.layerFlags = PSPLayerProperties.None;
            if (layer.Visible)
            {
                this.layerFlags |= PSPLayerProperties.Visible;
            }
            this.protectedTransparency = 0;
            this.linkGroup = 0;
            this.maskRect = Rectangle.Empty;
            this.saveMaskRect = Rectangle.Empty;
            this.maskLinked = 0;
            this.invertMaskOnBlend = 0;
            this.blendRangeCount = 0;
            this.blendRanges = new BlendRange[BlendRangeCount];
            this.v5BitmapCount = 0;
            this.v5ChannelCount = 0;
            this.useHighlightColor = 0;
            this.highlightColor = 0;
        }

        public void Save(BinaryWriterEx bw, ushort majorVersion)
        {
            if (majorVersion > PSPConstants.majorVersion5)
            {
                bw.Write(this.chunkSize);
            }
            WriteLayerName(bw, majorVersion);
            bw.Write((byte)this.type);
            bw.Write(this.imageRect);
            bw.Write(this.saveRect);
            bw.Write(this.opacity);
            bw.Write((byte)this.blendMode);
            bw.Write((byte)this.layerFlags);
            bw.Write(this.protectedTransparency);
            bw.Write(this.linkGroup);
            bw.Write(this.maskRect);
            bw.Write(this.saveMaskRect);
            bw.Write(this.maskLinked);
            bw.Write(this.maskDisabled);
            bw.Write(this.invertMaskOnBlend);
            bw.Write(this.blendRangeCount);
            for (int i = 0; i < this.blendRanges.Length; i++)
            {
                this.blendRanges[i].Write(bw);
            }
            if (majorVersion <= PSPConstants.majorVersion5)
            {
                bw.Write(this.v5BitmapCount);
                bw.Write(this.v5ChannelCount);
            }
        }

        [SkipLocalsInit]
        private void WriteLayerName(BinaryWriter writer, ushort majorVersion)
        {
            const int MaxStackBufferLength = 256;

            Span<byte> buffer = stackalloc byte[MaxStackBufferLength];

            byte[]? arrayFromPool = null;

            try
            {
                int maxNameLengthInBytes = Encoding.ASCII.GetMaxByteCount(this.name.Length);

                if (maxNameLengthInBytes > MaxStackBufferLength)
                {
                    arrayFromPool = ArrayPool<byte>.Shared.Rent(maxNameLengthInBytes);
                    buffer = arrayFromPool;
                }

                int bytesWritten = Encoding.ASCII.GetBytes(this.name, buffer);

                if (majorVersion > PSPConstants.majorVersion5)
                {
                    buffer = buffer.Slice(0, bytesWritten);

                    writer.Write(checked((ushort)buffer.Length));
                    writer.Write(buffer);
                }
                else
                {
                    // Paint Shop Pro 5 and earlier use a null-terminated layer name string in a fixed 256 byte
                    // buffer, so strings that are longer than 255 bytes will be truncated to ensure that the
                    // null-terminator can be written.
                    buffer = buffer.Slice(0, 256);

                    int terminatorIndex = Math.Min(bytesWritten, 255);

                    // Fill the remaining space in the buffer with zeros.
                    buffer.Slice(terminatorIndex).Clear();

                    writer.Write(buffer);
                }
            }
            finally
            {
                if (arrayFromPool != null)
                {
                    ArrayPool<byte>.Shared.Return(arrayFromPool);
                }
            }
        }
    }

    internal sealed class LayerBlock
    {
#pragma warning disable IDE0032 // Use auto property
        private readonly LayerInfoChunk[] layerInfoChunks;
        private readonly LayerBitmapInfoChunk[] layerBitmapInfo;
#pragma warning restore IDE0032 // Use auto property

        public LayerInfoChunk[] LayerInfo
        {
            get
            {
                return this.layerInfoChunks;
            }
        }

        public LayerBitmapInfoChunk[] LayerBitmapInfo
        {
            get
            {
                return this.layerBitmapInfo;
            }
        }

        public LayerBlock(LayerInfoChunk[] infoChunks, LayerBitmapInfoChunk[] biChunks)
        {
            this.layerInfoChunks = infoChunks;
            this.layerBitmapInfo = biChunks;
        }

        public LayerBlock(EndianBinaryReader br, GeneralImageAttributes? imageAttributes, ushort majorVersion)
        {
            if (imageAttributes is null)
            {
                // The image attributes block must come before the layer block.
                throw new FormatException(Properties.Resources.InvalidPSPFile);
            }

            IList<RasterLayerChunk> raster = CountRasterChunks(br, imageAttributes.LayerCount, majorVersion);

            int layerCount = raster.Count;

            if (layerCount == 0)
            {
                throw new FormatException(Properties.Resources.RasterLayerNotFound);
            }

            this.layerInfoChunks = new LayerInfoChunk[layerCount];
            this.layerBitmapInfo = new LayerBitmapInfoChunk[layerCount];
            PSPCompression compression = imageAttributes.CompressionType;

            for (int i = 0; i < layerCount; i++)
            {
                RasterLayerChunk chunk = raster[i];
                this.layerInfoChunks[i] = chunk.layerInfo;

                if (!chunk.layerInfo.saveRect.IsEmpty)
                {
                    br.Position = chunk.bitmapInfoOffset;
                    if (majorVersion <= PSPConstants.majorVersion5)
                    {
                        this.layerBitmapInfo[i] = new LayerBitmapInfoChunk(br, compression, chunk.layerInfo.v5BitmapCount, chunk.layerInfo.v5ChannelCount);
                    }
                    else
                    {
                        this.layerBitmapInfo[i] = new LayerBitmapInfoChunk(br, compression, majorVersion);
                    }
                }
            }
        }

        public void Save(BinaryWriterEx bw, ushort majorVersion)
        {
            bw.Write(PSPConstants.blockIdentifier);
            bw.Write((ushort)PSPBlockID.LayerStart);
            if (majorVersion <= PSPConstants.majorVersion5)
            {
                bw.Write(0U); // Initial data chunk length, always 0.
            }

            using (new BlockLengthWriter(bw))
            {
                int count = this.layerBitmapInfo.Length;

                for (int i = 0; i < count; i++)
                {
                    bw.Write(PSPConstants.blockIdentifier);
                    bw.Write((ushort)PSPBlockID.Layer);
                    if (majorVersion <= PSPConstants.majorVersion5)
                    {
                        bw.Write(LayerInfoChunk.Version5ChunkLength); // Initial data chunk length.
                    }

                    using (new BlockLengthWriter(bw))
                    {
                        this.layerInfoChunks[i].Save(bw, majorVersion);
                        this.layerBitmapInfo[i].Save(bw, majorVersion);
                    }
                }
            }
        }

        private sealed class RasterLayerChunk
        {
            public readonly LayerInfoChunk layerInfo;
            public readonly long bitmapInfoOffset;

            public RasterLayerChunk(LayerInfoChunk info, long offset)
            {
                this.layerInfo = info;
                this.bitmapInfoOffset = offset;
            }
        }

        private static IList<RasterLayerChunk> CountRasterChunks(EndianBinaryReader reader, int layerCount, ushort majorVersion)
        {
            List<RasterLayerChunk> rasterChunks = new List<RasterLayerChunk>(layerCount);

            int index = 0;
            while (index < layerCount)
            {
                uint head = reader.ReadUInt32();
                if (head != PSPConstants.blockIdentifier)
                {
                    throw new FormatException(Properties.Resources.InvalidBlockSignature);
                }
                PSPBlockID blockID = (PSPBlockID)reader.ReadUInt16();
#pragma warning disable IDE0059 // Value assigned to symbol is never used
                uint initialBlockLength = majorVersion <= PSPConstants.majorVersion5 ? reader.ReadUInt32() : 0;
#pragma warning restore IDE0059 // Value assigned to symbol is never used
                uint blockLength = reader.ReadUInt32();

                if (blockID == PSPBlockID.Layer)
                {
                    index++;
                    long endOffset = reader.Position + blockLength;

                    LayerInfoChunk chunk = new LayerInfoChunk(reader, majorVersion);
                    long currentOffset = reader.Position;

                    switch (chunk.type)
                    {
                        case PSPLayerType.Raster:
                        case PSPLayerType.FloatingRasterSelection:
                            if (majorVersion >= PSPConstants.majorVersion12)
                            {
                                // Paint Shop Pro X2 and later insert an unknown block (0x21) before the start of the LayerBitmapInfo chunk.
                                bool ok = false;
                                if (reader.ReadUInt32() == PSPConstants.blockIdentifier)
                                {
                                    ushort block = reader.ReadUInt16();
                                    uint length = reader.ReadUInt32();

                                    if (block == 0x21)
                                    {
                                        reader.Position += length;
                                        if (reader.ReadUInt32() == LayerBitmapInfoChunk.HeaderSize)
                                        {
                                            reader.Position -= 4L;
                                            currentOffset = reader.Position;
                                            ok = true;
                                        }
                                    }
                                }

                                if (!ok)
                                {
                                    throw new FormatException(Properties.Resources.UnsupportedFormatVersion);
                                }
                            }

                            rasterChunks.Add(new RasterLayerChunk(chunk, currentOffset));
                            break;
                    }

                    reader.Position += (endOffset - currentOffset);
                }
                else
                {
                    reader.Position += blockLength;
                }
            }

            return rasterChunks;
        }
    }
}
