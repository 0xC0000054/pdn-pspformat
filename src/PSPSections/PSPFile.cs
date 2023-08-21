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

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See src/Resources/Files/License.txt for full licensing and attribution      //
// details.                                                                    //
// .                                                                           //
/////////////////////////////////////////////////////////////////////////////////

using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using PaintShopProFiletype.PSPSections;

namespace PaintShopProFiletype
{
    internal sealed class PSPFile
    {
        private static ReadOnlySpan<byte> PSPFileSig => new byte[32]
        { 0x50, 0x61, 0x69, 0x6E, 0x74, 0x20, 0x53, 0x68, 0x6F, 0x70, 0x20, 0x50,
        0x72, 0x6F, 0x20, 0x49, 0x6D, 0x61, 0x67, 0x65, 0x20, 0x46, 0x69, 0x6C, 0x65, 0x0A,
        0x1A, 0x00, 0x00, 0x00, 0x00, 0x00}; // "Paint Shop Pro Image File\n\x1a” padded to 32 bytes

        private FileHeader fileHeader;
        private GeneralImageAttributes imageAttributes;
        private ExtendedDataBlock extData;
        private CreatorBlock creator;
        private CompositeImageBlock compImage;
        private ColorPaletteBlock globalPalette;
        private LayerBlock layerBlock;
        private ThumbnailBlock v5Thumbnail;

        public PSPFile()
        {
            this.fileHeader = null;
            this.imageAttributes = null;
            this.extData = null;
            this.creator = null;
            this.compImage = null;
            this.globalPalette = null;
            this.layerBlock = null;
            this.v5Thumbnail = null;
        }

        [SkipLocalsInit]
        private static bool CheckFileSignature(Stream input)
        {
            Span<byte> signature = stackalloc byte[32];

            input.ReadExactly(signature);

            // Some writers may not write zeros for the signature padding, so we only check the first 27 bytes of the signature.
            const int SignatureCheckLength = 27;

            return signature.Slice(0, SignatureCheckLength).SequenceEqual(PSPFileSig.Slice(0, SignatureCheckLength));
        }

        private static LayerBlendMode ConvertFromPSPBlendMode(PSPBlendModes mode)
        {
            return mode switch
            {
                PSPBlendModes.Normal => LayerBlendMode.Normal,
                PSPBlendModes.Darken => LayerBlendMode.Darken,
                PSPBlendModes.Lighten => LayerBlendMode.Lighten,
                PSPBlendModes.Multiply => LayerBlendMode.Multiply,
                PSPBlendModes.Screen => LayerBlendMode.Screen,
                PSPBlendModes.Overlay => LayerBlendMode.Overlay,
                PSPBlendModes.Difference => LayerBlendMode.Difference,
                PSPBlendModes.Dodge => LayerBlendMode.ColorDodge,
                PSPBlendModes.Burn => LayerBlendMode.ColorBurn,
                _ => LayerBlendMode.Normal,
            };
        }

        private void LoadPSPFile(Stream input)
        {
            if (!CheckFileSignature(input))
            {
                throw new FormatException(Properties.Resources.InvalidPSPFile);
            }

            using (BufferedBinaryReader reader = new BufferedBinaryReader(input))
            {
                this.fileHeader = new FileHeader(reader);

                while (reader.Position < reader.Length)
                {
                    uint blockSig = reader.ReadUInt32();
                    if (blockSig != PSPConstants.blockIdentifier)
                    {
                        throw new FormatException(Properties.Resources.InvalidBlockSignature);
                    }
                    PSPBlockID blockID = (PSPBlockID)reader.ReadUInt16();
                    uint initialBlockLength = this.fileHeader.Major <= PSPConstants.majorVersion5 ? reader.ReadUInt32() : 0;
                    uint blockLength = reader.ReadUInt32();

                    switch (blockID)
                    {
                        case PSPBlockID.ImageAttributes:
                            this.imageAttributes = new GeneralImageAttributes(reader, this.fileHeader.Major);
                            break;
                        case PSPBlockID.Creator:
                            this.creator = new CreatorBlock(reader, blockLength);
                            break;
                        case PSPBlockID.ColorPalette:
                            this.globalPalette = new ColorPaletteBlock(reader, this.fileHeader.Major);
                            break;
                        case PSPBlockID.LayerStart:
                            this.layerBlock = new LayerBlock(reader, this.imageAttributes, this.fileHeader.Major);
                            break;
                        case PSPBlockID.ExtendedData:
                            this.extData = new ExtendedDataBlock(reader, blockLength);
                            break;
#if DEBUG
                        case PSPBlockID.CompositeImageBank:
                            this.compImage = new CompositeImageBlock(reader, this.fileHeader.Major);
                            break;
#endif
                        default:
                            reader.Position += blockLength;
                            break;
                    }
                }
            }
        }

        private static unsafe byte[] ExpandPackedPalette(Rectangle saveRect, LayerBitmapInfoChunk bitmap, int bitDepth)
        {
            int height = saveRect.Height;
            int width = saveRect.Width;
            byte[] image = new byte[width * height];

            int bpp, shift;

            switch (bitDepth)
            {
                case 4:
                    bpp = 2;
                    shift = 1;
                    break;
                case 1:
                    bpp = 8;
                    shift = 3;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("bitDepth", string.Format("Bit depth value of {0} is not supported, value must be 1 or 4.", bitDepth));
            }

            fixed (byte* ptr = image, dPtr = bitmap.channels[0].channelData)
            {
                int srcStride = width / bpp;

                for (int y = 0; y < height; y++)
                {
                    byte* src = dPtr + (y * srcStride);
                    byte* dst = ptr + (y * width);

                    for (int x = 0; x < width; x++)
                    {
                        byte data = src[x >> shift];

                        switch (bitDepth)
                        {
                            case 4:

                                if ((x & 1) == 1) // odd column
                                {
                                    *dst = (byte)(data & 0x0f);
                                }
                                else
                                {
                                    *dst = (byte)(data >> 4);
                                }

                                break;
                            case 1:

                                int mask = x & 7;
                                // extract the palette bit for the current pixel.
                                *dst = (byte)((data & (128 >> mask)) >> (7 - mask));
                                break;
                        }

                        dst++;
                    }
                }
            }

            return image;
        }

        public Document Load(Stream input)
        {
            LoadPSPFile(input);

            if (this.imageAttributes == null || this.layerBlock == null)
            {
                throw new FormatException(Properties.Resources.InvalidPSPFile);
            }

            if (this.imageAttributes.BitDepth <= 8 && this.globalPalette == null)
            {
                throw new FormatException(Properties.Resources.ColorPaletteNotFound);
            }

            LayerInfoChunk[] infoChunks = this.layerBlock.LayerInfo;
            int layerCount = infoChunks.Length;

            // Some PSP files may have layers that are larger than the dimensions stored in the image attributes.
            int maxWidth = this.imageAttributes.Width;
            int maxHeight = this.imageAttributes.Height;

            for (int i = 0; i < layerCount; i++)
            {
                Rectangle savedBounds = infoChunks[i].saveRect;
                if (savedBounds.Width > maxWidth)
                {
                    maxWidth = savedBounds.Width;
                }
                if (savedBounds.Height > maxHeight)
                {
                    maxHeight = savedBounds.Height;
                }
            }

            if (maxWidth <= 0 || maxHeight <= 0)
            {
                throw new FormatException(Properties.Resources.InvalidDocumentDimensions);
            }

            Document doc = new Document(maxWidth, maxHeight);

            if (this.imageAttributes.ResValue > 0.0)
            {
                switch (this.imageAttributes.ResUnit)
                {
                    case ResolutionMetric.Inch:
                        doc.DpuUnit = MeasurementUnit.Inch;
                        doc.DpuX = doc.DpuY = this.imageAttributes.ResValue;
                        break;
                    case ResolutionMetric.Centimeter:
                        doc.DpuUnit = MeasurementUnit.Centimeter;
                        doc.DpuX = doc.DpuY = this.imageAttributes.ResValue;
                        break;
                    default:
                        break;
                }
            }

            short transIndex = -1;

            if (this.imageAttributes.BitDepth < 24 && this.extData != null)
            {
                transIndex = this.extData.TryGetTransparencyIndex();
            }

            LayerBitmapInfoChunk[] bitmapInfoChunks = this.layerBlock.LayerBitmapInfo;

            for (int i = 0; i < layerCount; i++)
            {
                LayerInfoChunk info = infoChunks[i];

                BitmapLayer layer = new BitmapLayer(doc.Width, doc.Height)
                {
                    Name = info.name,
                    Opacity = info.opacity,
                    BlendMode = ConvertFromPSPBlendMode(info.blendMode),
                    Visible = (info.layerFlags & PSPLayerProperties.Visible) == PSPLayerProperties.Visible
                };

                Rectangle saveRect = info.saveRect;

                if (!saveRect.IsEmpty)
                {
                    LayerBitmapInfoChunk bitmapInfo = bitmapInfoChunks[i];

                    if (bitmapInfo.bitmapCount == 1)
                    {
                        new UnaryPixelOps.SetAlphaChannelTo255().Apply(layer.Surface, saveRect);
                    }

                    int alphaIndex = 0;

                    int bitDepth = this.imageAttributes.BitDepth;

                    int bytesPerPixel = 1;
                    int stride = saveRect.Width;
                    byte[] expandedPalette = null;

                    switch (bitDepth)
                    {
                        case 48:
                            bytesPerPixel = 2;
                            stride *= 2;
                            break;
                        case 4:
                        case 1:
                            expandedPalette = ExpandPackedPalette(saveRect, bitmapInfo, bitDepth);
                            break;
                    }

                    unsafe
                    {
                        int palIdx = 0;
                        NativeStructs.RGBQUAD entry;

                        Surface surface = layer.Surface;
                        for (int y = saveRect.Top; y < saveRect.Bottom; y++)
                        {
                            ColorBgra* ptr = surface.GetPointPointerUnchecked(saveRect.Left, y);
                            ColorBgra* endPtr = ptr + saveRect.Width;
                            int index = ((y - saveRect.Top) * stride);

                            while (ptr < endPtr)
                            {
                                switch (bitDepth)
                                {
                                    case 48:

                                        for (int ci = 0; ci < bitmapInfo.channelCount; ci++)
                                        {
                                            ChannelSubBlock ch = bitmapInfo.channels[ci];

                                            if (ch.bitmapType == PSPDIBType.Image)
                                            {
                                                ushort col = (ushort)(ch.channelData[index] | (ch.channelData[index + 1] << 8)); // PSP format is always little endian
                                                byte clamped = (byte)((col * 255) / 65535);

                                                switch (ch.channelType)
                                                {
                                                    case PSPChannelType.Red:
                                                        ptr->R = clamped;
                                                        break;
                                                    case PSPChannelType.Green:
                                                        ptr->G = clamped;
                                                        break;
                                                    case PSPChannelType.Blue:
                                                        ptr->B = clamped;
                                                        break;
                                                }
                                            }
                                            else if (ch.bitmapType == PSPDIBType.TransparencyMask)
                                            {
                                                ptr->A = ch.channelData[alphaIndex];
                                                alphaIndex++;
                                            }
                                        }

                                        break;
                                    case 24:

                                        for (int ci = 0; ci < bitmapInfo.channelCount; ci++)
                                        {
                                            ChannelSubBlock ch = bitmapInfo.channels[ci];

                                            if (ch.bitmapType == PSPDIBType.Image)
                                            {
                                                switch (ch.channelType)
                                                {
                                                    case PSPChannelType.Red:
                                                        ptr->R = ch.channelData[index];
                                                        break;
                                                    case PSPChannelType.Green:
                                                        ptr->G = ch.channelData[index];
                                                        break;
                                                    case PSPChannelType.Blue:
                                                        ptr->B = ch.channelData[index];
                                                        break;
                                                }
                                            }
                                            else if (ch.bitmapType == PSPDIBType.TransparencyMask)
                                            {
                                                ptr->A = ch.channelData[index];
                                            }
                                        }

                                        break;
                                    case 8:
                                        for (int ci = 0; ci < bitmapInfo.channelCount; ci++)
                                        {
                                            ChannelSubBlock ch = bitmapInfo.channels[ci];
                                            palIdx = ch.channelData[index];
                                            entry = this.globalPalette.entries[palIdx];

                                            switch (ch.bitmapType)
                                            {
                                                case PSPDIBType.Image:
                                                    ptr->R = entry.rgbRed;
                                                    ptr->G = entry.rgbGreen;
                                                    ptr->B = entry.rgbBlue;

                                                    if (palIdx == transIndex)
                                                    {
                                                        ptr->A = 0;
                                                    }

                                                    break;
                                                case PSPDIBType.TransparencyMask:
                                                    ptr->A = entry.rgbRed;
                                                    break;
                                            }
                                        }

                                        break;
                                    case 4:
                                    case 1:

                                        palIdx = expandedPalette[index];
                                        entry = this.globalPalette.entries[palIdx];

                                        ptr->R = entry.rgbRed;
                                        ptr->G = entry.rgbGreen;
                                        ptr->B = entry.rgbBlue;

                                        if (palIdx == transIndex)
                                        {
                                            ptr->A = 0;
                                        }
                                        else if ((bitmapInfo.bitmapCount == 2) && bitmapInfo.channels[1].bitmapType == PSPDIBType.TransparencyMask)
                                        {
                                            ptr->A = bitmapInfo.channels[1].channelData[index];
                                        }

                                        break;
                                    default:
                                        throw new FormatException(string.Format(Properties.Resources.UnsupportedBitDepth, bitDepth));

                                }

                                ptr++;
                                index += bytesPerPixel;
                            }
                        }
                    }
                }

#if DEBUG
                using (Bitmap temp = layer.Surface.CreateAliasedBitmap())
                {
                }
#endif
                doc.Layers.Add(layer);
            }

            CreatorBlockSerialization.Serialize(this.creator, doc);

            return doc;
        }

        private static PSPBlendModes ConvertToPSPBlendMode(LayerBlendMode blendMode)
        {
            return blendMode switch
            {
                LayerBlendMode.Normal => PSPBlendModes.Normal,
                LayerBlendMode.Multiply => PSPBlendModes.Multiply,
                LayerBlendMode.ColorBurn => PSPBlendModes.Burn,
                LayerBlendMode.ColorDodge => PSPBlendModes.Dodge,
                LayerBlendMode.Overlay => PSPBlendModes.Overlay,
                LayerBlendMode.Difference => PSPBlendModes.Difference,
                LayerBlendMode.Lighten => PSPBlendModes.Lighten,
                LayerBlendMode.Darken => PSPBlendModes.Darken,
                LayerBlendMode.Screen => PSPBlendModes.Screen,
                _ => PSPBlendModes.Normal,
            };
        }

        private static PSPCompression CompressionFromTokenFormat(CompressionFormats format)
        {
            PSPCompression comp = PSPCompression.None;

            switch (format)
            {
                case CompressionFormats.LZ77:
                    comp = PSPCompression.LZ77;
                    break;
            }

            return comp;
        }

        private int doneProgress;
        private int totalProgress;
        private unsafe ChannelSubBlock[] SplitImageChannels(
            Surface source,
            Rectangle savedBounds,
            int channelCount,
            ushort majorVersion,
            bool composite,
            ProgressEventHandler callback)
        {
            ChannelSubBlock[] channels = new ChannelSubBlock[channelCount];

            int channelSize = savedBounds.Width * savedBounds.Height;

            for (int i = 0; i < channelCount; i++)
            {
                channels[i] = new ChannelSubBlock(majorVersion, (uint)channelSize);
                if (composite)
                {
                    switch (majorVersion)
                    {
                        case PSPConstants.majorVersion5:
                            channels[i].bitmapType = PSPDIBType.Thumbnail;
                            break;
                        default:
                            channels[i].bitmapType = i < 3 ? PSPDIBType.Composite : PSPDIBType.CompositeTransparencyMask;
                            break;
                    }
                }
                else
                {
                    channels[i].bitmapType = i < 3 ? PSPDIBType.Image : PSPDIBType.TransparencyMask;
                }

                switch (i)
                {
                    case 0:
                        channels[i].channelType = PSPChannelType.Red;
                        break;
                    case 1:
                        channels[i].channelType = PSPChannelType.Green;
                        break;
                    case 2:
                        channels[i].channelType = PSPChannelType.Blue;
                        break;
                    case 3:
                        channels[i].channelType = PSPChannelType.Composite;
                        break;
                }
            }

            if (channelSize > 0)
            {
                RegionPtr<ColorBgra32> sourceRegion = source.AsRegionPtr().Slice(savedBounds).Cast<ColorBgra32>();

                using (SpanOwner<byte> uncompressedDataOwner = SpanOwner<byte>.Allocate(channelSize))
                {
                    Span<byte> uncompresedData = uncompressedDataOwner.Span;

                    fixed (byte* buffer = uncompresedData)
                    {
                        RegionPtr<byte> targetRegion = new RegionPtr<byte>(buffer, savedBounds.Size, savedBounds.Width);

                        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                        {
                            int sourceIndex = channelIndex;

                            // Map BGRA to RGBA
                            switch (channelIndex)
                            {
                                case 0:
                                    sourceIndex = 2;
                                    break;
                                case 2:
                                    sourceIndex = 0;
                                    break;
                            }

                            PixelKernels.ExtractChannel(targetRegion, sourceRegion, sourceIndex);

                            ChannelSubBlock channel = channels[channelIndex];

                            switch (this.imageAttributes.CompressionType)
                            {
                                case PSPCompression.None:
                                    channel.compressedChannelLength = (uint)channelSize;
                                    channel.uncompressedChannelLength = 0;
                                    channel.channelData = uncompresedData.ToArray();
                                    break;
                                case PSPCompression.LZ77:

                                    using (ArrayPoolBufferWriter<byte> bufferWriter = new ArrayPoolBufferWriter<byte>())
                                    {
                                        using (ZLibStream zs = new ZLibStream(bufferWriter.AsStream(), CompressionLevel.SmallestSize, true))
                                        {
                                            zs.Write(uncompresedData);
                                        }

                                        ReadOnlySpan<byte> compressedData = bufferWriter.WrittenSpan;
                                        channel.compressedChannelLength = (uint)compressedData.Length;
                                        channel.uncompressedChannelLength = (uint)channelSize;
                                        channel.channelData = compressedData.ToArray();
                                    }
                                    break;
                            }

                            if (callback != null)
                            {
                                this.doneProgress++;

                                callback(this, new ProgressEventArgs(100.0 * ((double)this.doneProgress / this.totalProgress)));
                            }
                        }
                    }
                }
            }

            return channels;
        }

        private static Size GetThumbnailDimensions(int originalWidth, int originalHeight, int maxEdgeLength)
        {
            Size thumbSize = Size.Empty;

            if (originalWidth <= 0 || originalHeight <= 0)
            {
                thumbSize.Width = 1;
                thumbSize.Height = 1;
            }
            else if (originalWidth > originalHeight)
            {
                int longSide = Math.Min(originalWidth, maxEdgeLength);
                thumbSize.Width = longSide;
                thumbSize.Height = Math.Max(1, (originalHeight * longSide) / originalWidth);
            }
            else if (originalHeight > originalWidth)
            {
                int longSide = Math.Min(originalHeight, maxEdgeLength);
                thumbSize.Width = Math.Max(1, (originalWidth * longSide) / originalHeight);
                thumbSize.Height = longSide;
            }
            else
            {
                int longSide = Math.Min(originalWidth, maxEdgeLength);
                thumbSize.Width = longSide;
                thumbSize.Height = longSide;
            }

            return thumbSize;
        }

        private static unsafe bool LayerHasTransparency(Surface surface, Rectangle bounds)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                ColorBgra* startPtr = surface.GetPointPointerUnchecked(bounds.Left, y);
                ColorBgra* endPtr = startPtr + bounds.Width;

                while (startPtr < endPtr)
                {
                    if (startPtr->A < 255)
                    {
                        return true;
                    }

                    startPtr++;
                }
            }

            return false;
        }

        public void Save(Document input, Stream output, CompressionFormats format, Surface scratchSurface, ushort majorVersion, ProgressEventHandler callback)
        {
            if (majorVersion == PSPConstants.majorVersion5 && input.Layers.Count > 64)
            {
                throw new FormatException(string.Format(Properties.Resources.MaxLayersFormat, 64));
            }
            else if ((majorVersion == PSPConstants.majorVersion6 || majorVersion == PSPConstants.majorVersion7) && input.Layers.Count > 100)
            {
                throw new FormatException(string.Format(Properties.Resources.MaxLayersFormat, 100));
            }

            using (BinaryWriterEx writer = new BinaryWriterEx(output, false))
            {
                this.fileHeader = new FileHeader(majorVersion);
                this.imageAttributes = new GeneralImageAttributes(
                    input.Width,
                    input.Height,
                    CompressionFromTokenFormat(format),
                    0,
                    input.Layers.Count,
                    majorVersion);
                switch (input.DpuUnit)
                {
                    case MeasurementUnit.Centimeter:
                        this.imageAttributes.ResValue = input.DpuX;
                        this.imageAttributes.ResUnit = ResolutionMetric.Centimeter;
                        break;
                    case MeasurementUnit.Inch:
                        this.imageAttributes.ResValue = input.DpuX;
                        this.imageAttributes.ResUnit = ResolutionMetric.Inch;
                        break;
                }

                this.totalProgress = 0;
                this.doneProgress = 0;

                bool flatImage = true;
                foreach (Layer item in input.Layers)
                {
                    BitmapLayer layer = (BitmapLayer)item;

                    Rectangle rect = PSPUtil.GetImageSaveRectangle(layer.Surface);

                    if (!rect.IsEmpty)
                    {
                        if (LayerHasTransparency(layer.Surface, rect))
                        {
                            this.totalProgress += 4;
                            flatImage = false;
                        }
                        else
                        {
                            this.totalProgress += 3;
                        }
                    }

                }

                if (flatImage)
                {
                    this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.FlatImage);
                }

                if (majorVersion > PSPConstants.majorVersion5)
                {
                    CreateCompositeImageBlock(input, scratchSurface, callback, majorVersion);
                }
                else
                {
                    CreateThumbnailBlock(input, scratchSurface, callback, majorVersion);
                }

                int layerCount = input.Layers.Count;

                LayerInfoChunk[] layerInfoChunks = new LayerInfoChunk[layerCount];
                LayerBitmapInfoChunk[] layerBitmapChunks = new LayerBitmapInfoChunk[layerCount];

                for (int i = 0; i < layerCount; i++)
                {
                    BitmapLayer layer = (BitmapLayer)input.Layers[i];

                    Rectangle savedBounds = PSPUtil.GetImageSaveRectangle(layer.Surface);

                    LayerInfoChunk infoChunk = new LayerInfoChunk(layer, ConvertToPSPBlendMode(layer.BlendMode), savedBounds, majorVersion);

                    int channelCount = 3;
                    int bitmapCount = 1;

                    if (LayerHasTransparency(layer.Surface, savedBounds))
                    {
                        channelCount = 4;
                        bitmapCount = 2;
                    }

                    LayerBitmapInfoChunk biChunk = new LayerBitmapInfoChunk(bitmapCount, channelCount)
                    {
                        channels = SplitImageChannels(layer.Surface, savedBounds, channelCount, majorVersion, false, callback)
                    };

                    if (majorVersion <= PSPConstants.majorVersion5)
                    {
                        infoChunk.v5BitmapCount = biChunk.bitmapCount;
                        infoChunk.v5ChannelCount = biChunk.channelCount;
                    }

                    layerInfoChunks[i] = infoChunk;
                    layerBitmapChunks[i] = biChunk;
                }

                this.layerBlock = new LayerBlock(layerInfoChunks, layerBitmapChunks);
                this.creator = CreatorBlockSerialization.Deserialize(input);

                writer.Write(PSPFileSig);
                this.fileHeader.Save(writer);
                this.imageAttributes.Save(writer);
                this.creator.Save(writer);
                if (this.compImage != null)
                {
                    this.compImage.Save(writer);
                }
                else if (this.v5Thumbnail != null)
                {
                    this.v5Thumbnail.Save(writer);
                }
                this.layerBlock.Save(writer, majorVersion);
            }
        }

        private void CreateCompositeImageBlock(Document input, Surface scratchSurface, ProgressEventHandler callback, ushort majorVersion)
        {
            Size jpegThumbSize = GetThumbnailDimensions(input.Width, input.Height, 200);

            CompositeImageAttributesChunk normAttr = new CompositeImageAttributesChunk(
                input.Width,
                input.Height,
                PSPCompositeImageType.Composite,
                this.imageAttributes.CompressionType);
            CompositeImageAttributesChunk jpgAttr = new CompositeImageAttributesChunk(
                jpegThumbSize.Width,
                jpegThumbSize.Height,
                PSPCompositeImageType.Thumbnail,
                PSPCompression.JPEG);
            JPEGCompositeInfoChunk jpgChunk = new JPEGCompositeInfoChunk();
            CompositeImageInfoChunk infoChunk = new CompositeImageInfoChunk();

            scratchSurface.Fill(ColorBgra.TransparentBlack);
            input.CreateRenderer().Render(scratchSurface);

            int channelCount = 3;
            if (LayerHasTransparency(scratchSurface, scratchSurface.Bounds))
            {
                channelCount = 4;
                infoChunk.channelCount = 4;
                infoChunk.bitmapCount = 2;
            }
#if DEBUG
            using (Bitmap bmp = scratchSurface.CreateAliasedBitmap())
            {
            }
#endif

            this.totalProgress += channelCount;

            infoChunk.channelBlocks = SplitImageChannels(scratchSurface, scratchSurface.Bounds, channelCount, majorVersion, true, callback);

            using (Surface fit = new Surface(jpegThumbSize))
            {
                fit.FitSurface(ResamplingAlgorithm.SuperSampling, scratchSurface);

                if (channelCount == 4)
                {
                    unsafe
                    {
                        for (int y = 0; y < jpgAttr.height; y++)
                        {
                            ColorBgra* ptr = fit.GetRowPointerUnchecked(y);
                            ColorBgra* endPtr = ptr + jpgAttr.width;

                            while (ptr < endPtr)
                            {
                                if (ptr->A == 0)
                                {
                                    ptr->Bgra |= 0x00ffffff; // set the color of the transparent pixels to white, same as Paint Shop Pro
                                }

                                ptr++;
                            }
                        }
                    }
                }

                using (Bitmap temp = fit.CreateAliasedBitmap(false))
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        temp.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);

                        jpgChunk.imageData = stream.ToArray();
                        jpgChunk.compressedSize = (uint)stream.Length;
                    }
                }
            }

            this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.Composite);
            this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.Thumbnail);

            if (infoChunk.bitmapCount == 2)
            {
                this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.CompositeTransparency);
            }

            this.compImage = new CompositeImageBlock(new CompositeImageAttributesChunk[2] { jpgAttr, normAttr }, jpgChunk, infoChunk);
        }

        private void CreateThumbnailBlock(Document input, Surface scratchSurface, ProgressEventHandler callback, ushort majorVersion)
        {
            Size thumbSize = GetThumbnailDimensions(input.Width, input.Height, 300);

            this.v5Thumbnail = new ThumbnailBlock(thumbSize.Width, thumbSize.Height);

            scratchSurface.Fill(ColorBgra.White);
            input.CreateRenderer().Render(scratchSurface);

            using (Surface fit = new Surface(thumbSize))
            {
                fit.FitSurface(ResamplingAlgorithm.SuperSampling, scratchSurface);

                this.totalProgress += 3;

                this.v5Thumbnail.channelBlocks = SplitImageChannels(scratchSurface, scratchSurface.Bounds, 3, majorVersion, true, callback);
            }
        }
    }
}
