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

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See src/Resources/Files/License.txt for full licensing and attribution      //
// details.                                                                    //
// .                                                                           //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Ionic.Zlib;
using PaintDotNet;
using PaintDotNet.IO;
using PaintShopProFiletype.PSPSections;

namespace PaintShopProFiletype
{
    internal sealed class PSPFile
    {
        private static byte[] PSPFileSig = new byte[32] {0x50, 0x61, 0x69, 0x6E, 0x74, 0x20, 0x53, 0x68, 0x6F, 0x70, 0x20, 0x50,
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

        private const string PSPCreatorMetaData = "PSPFormatCreatorData";

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

        private static string SerializeToBase64(object data)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(stream, data);

                return Convert.ToBase64String(stream.ToArray());
            }
        }

        /// <summary>
        /// Binds the serialization to types in the currently loaded assembly.
        /// </summary>
        private class SelfBinder : System.Runtime.Serialization.SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                return Type.GetType(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", typeName, assemblyName));
            }
        }

        private static T DeserializeFromBase64<T>(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);

            using (MemoryStream stream = new MemoryStream(bytes))
            {
                BinaryFormatter formatter = new BinaryFormatter() { Binder = new SelfBinder() };
                return (T)formatter.Deserialize(stream);
            }
        }

        private static bool CheckSig(byte[] sig)
        {
            // Some writers may not write zeros for the signature padding, so we only check the first 27 bytes of the signature.

            for (int i = 0; i < 27; i++)
            {
                if (sig[i] != PSPFileSig[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static UserBlendOp BlendModetoBlendOp(PSPBlendModes mode)
        {
            switch (mode)
            {
                case PSPBlendModes.Normal:
                    return new UserBlendOps.NormalBlendOp();
                case PSPBlendModes.Darken:
                    return new UserBlendOps.DarkenBlendOp();
                case PSPBlendModes.Lighten:
                    return new UserBlendOps.LightenBlendOp();
                case PSPBlendModes.Multiply:
                    return new UserBlendOps.MultiplyBlendOp();
                case PSPBlendModes.Screen:
                    return new UserBlendOps.ScreenBlendOp();
                case PSPBlendModes.Overlay:
                    return new UserBlendOps.OverlayBlendOp();
                case PSPBlendModes.Difference:
                    return new UserBlendOps.DifferenceBlendOp();
                case PSPBlendModes.Dodge:
                    return new UserBlendOps.ColorDodgeBlendOp();
                case PSPBlendModes.Burn:
                    return new UserBlendOps.ColorBurnBlendOp();
                default:
                    return new UserBlendOps.NormalBlendOp();
            }
        }

        private void LoadPSPFile(Stream input)
        {
            byte[] sigBytes = new byte[32];

            input.ProperRead(sigBytes, 0, sigBytes.Length);

            if (!CheckSig(sigBytes))
            {
                throw new FormatException(Properties.Resources.InvalidPSPFile);
            }

            using (BinaryReader reader = new BinaryReader(input))
            {
                this.fileHeader = new FileHeader(reader);

                while (input.Position < input.Length)
                {
                    uint blockSig = reader.ReadUInt32();
                    if (blockSig != PSPConstants.blockIdentifier)
                    {
                        throw new FormatException(Properties.Resources.InvalidBlockSignature);
                    }
                    PSPBlockID blockID = (PSPBlockID)reader.ReadUInt16();
                    uint initialBlockLength = fileHeader.Major <= PSPConstants.majorVersion5 ? reader.ReadUInt32() : 0;
                    uint blockLength = reader.ReadUInt32();

                    switch (blockID)
                    {
                        case PSPBlockID.ImageAttributes:
                            this.imageAttributes = new GeneralImageAttributes(reader, fileHeader.Major);
                            break;
                        case PSPBlockID.Creator:
                            this.creator = new CreatorBlock(reader, blockLength);
                            break;
                        case PSPBlockID.ColorPalette:
                            this.globalPalette = new ColorPaletteBlock(reader, fileHeader.Major);
                            break;
                        case PSPBlockID.LayerStart:
                            this.layerBlock = new LayerBlock(reader, imageAttributes, fileHeader.Major);
                            break;
                        case PSPBlockID.ExtendedData:
                            this.extData = new ExtendedDataBlock(reader, blockLength);
                            break;
#if DEBUG
                        case PSPBlockID.CompositeImageBank:
                            this.compImage = new CompositeImageBlock(reader, fileHeader.Major);
                            break;
#endif
                        default:
                            reader.BaseStream.Position += (long)blockLength;
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

            LayerBitmapInfoChunk[] bitmapInfoChunks =  this.layerBlock.LayerBitmapInfo;

            for (int i = 0; i < layerCount; i++)
            {
                LayerInfoChunk info = infoChunks[i];

                BitmapLayer layer = new BitmapLayer(doc.Width, doc.Height) { Name = info.name, Opacity = info.opacity };
                UserBlendOp blendOp = BlendModetoBlendOp(info.blendMode);
                layer.SetBlendOp(blendOp);
                layer.Visible = (info.layerFlags & PSPLayerProperties.Visible) == PSPLayerProperties.Visible;

                Rectangle saveRect = info.saveRect;

                if (!saveRect.IsEmpty)
                {
                    LayerBitmapInfoChunk bitmapInfo = bitmapInfoChunks[i];

                    short transIndex = -1;

                    if (this.imageAttributes.BitDepth < 24 && this.extData != null)
                    {
                        byte[] data;
                        if (this.extData.Values.TryGetValue(PSPExtendedDataID.TransparencyIndex, out data))
                        {
                            transIndex = BitConverter.ToInt16(data, 0);
                        }
                    }

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
                            ColorBgra* ptr = surface.GetPointAddressUnchecked(saveRect.Left, y);
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

            string creatorData = SerializeToBase64(this.creator);
            doc.Metadata.SetUserValue(PSPCreatorMetaData, creatorData);

            return doc;
        }

        private static PSPBlendModes BlendOptoBlendMode(UserBlendOp op)
        {
            Type opType = op.GetType();

            if (opType == typeof(UserBlendOps.NormalBlendOp))
            {
                return PSPBlendModes.Normal;
            }
            else if (opType == typeof(UserBlendOps.ColorBurnBlendOp))
            {
                return PSPBlendModes.Burn;
            }
            else if (opType == typeof(UserBlendOps.ColorDodgeBlendOp))
            {
                return PSPBlendModes.Dodge;
            }
            else if (opType == typeof(UserBlendOps.DarkenBlendOp))
            {
                return PSPBlendModes.Darken;
            }
            else if (opType == typeof(UserBlendOps.DifferenceBlendOp))
            {
                return PSPBlendModes.Difference;
            }
            else if (opType == typeof(UserBlendOps.LightenBlendOp))
            {
                return PSPBlendModes.Lighten;
            }
            else if (opType == typeof(UserBlendOps.MultiplyBlendOp))
            {
                return PSPBlendModes.Multiply;
            }
            else if (opType == typeof(UserBlendOps.OverlayBlendOp))
            {
                return PSPBlendModes.Overlay;
            }
            else if (opType == typeof(UserBlendOps.ScreenBlendOp))
            {
                return PSPBlendModes.Screen;
            }

            return PSPBlendModes.Normal;
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
        private unsafe ChannelSubBlock[] SplitImageChannels(Surface source, Rectangle savedBounds, int channelCount, ushort majorVersion, bool composite, ProgressEventHandler callback)
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
                byte[] red = new byte[channelSize];
                byte[] green = new byte[channelSize];
                byte[] blue = new byte[channelSize];
                byte[] alpha = channelCount == 4 ? new byte[channelSize] : null;

                for (int y = savedBounds.Top; y < savedBounds.Bottom; y++)
                {
                    ColorBgra* p = source.GetPointAddressUnchecked(savedBounds.Left, y);
                    int index = ((y - savedBounds.Top) * savedBounds.Width);
                    for (int x = savedBounds.Left; x < savedBounds.Right; x++)
                    {
                        red[index] = p->R;
                        green[index] = p->G;
                        blue[index] = p->B;
                        if (channelCount == 4)
                        {
                            alpha[index] = p->A;
                        }
                        p++;
                        index++;
                    }
                }

                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    byte[] inData = null;

                    switch (channelIndex)
                    {
                        case 0:
                            inData = red;
                            break;
                        case 1:
                            inData = green;
                            break;
                        case 2:
                            inData = blue;
                            break;
                        case 3:
                            inData = alpha;
                            break;
                    }


                    byte[] compBuffer = null;

                    switch (imageAttributes.CompressionType)
                    {
                        case PSPCompression.None:
                            compBuffer = inData;
                            channels[channelIndex].uncompressedChannelLength = 0;
                            break;
                        case PSPCompression.LZ77:

                            using (MemoryStream ms = new MemoryStream())
                            {
                                using (ZlibStream zs = new ZlibStream(ms, CompressionMode.Compress, CompressionLevel.Level9, true))
                                {
                                    int length = inData.Length;
                                    int offset = 0;

                                    do
                                    {
                                        int chunkSize = Math.Min(ZlibConstants.WorkingBufferSizeDefault, length);

                                        zs.Write(inData, offset, chunkSize);

                                        offset += chunkSize;
                                        length -= chunkSize;

                                    } while (length > 0);

                                }
                                compBuffer = ms.ToArray();
                            }
                            break;
                    }

                    if (callback != null)
                    {
                        doneProgress++;

                        callback(this, new ProgressEventArgs(100.0 * ((double)doneProgress / (double)totalProgress)));
                    }

                    channels[channelIndex].compressedChannelLength = (uint)compBuffer.Length;
                    channels[channelIndex].channelData = compBuffer;

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
                thumbSize.Height= longSide;
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
                ColorBgra* startPtr = surface.GetPointAddressUnchecked(bounds.Left, y);
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
                this.imageAttributes = new GeneralImageAttributes(input.Width, input.Height, CompressionFromTokenFormat(format), 0, input.Layers.Count, majorVersion);
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
                            totalProgress += 4;
                            flatImage = false;
                        }
                        else
                        {
                            totalProgress += 3;
                        }
                    }

                }

                if (flatImage)
                {
                    this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.FlatImage);
                }

                if (majorVersion > PSPConstants.majorVersion5)
                {
                    Size jpegThumbSize = GetThumbnailDimensions(input.Width, input.Height, 200);

                    CompositeImageAttributesChunk normAttr = new CompositeImageAttributesChunk(input.Width, input.Height, PSPCompositeImageType.Composite, imageAttributes.CompressionType);
                    CompositeImageAttributesChunk jpgAttr = new CompositeImageAttributesChunk(jpegThumbSize.Width, jpegThumbSize.Height, PSPCompositeImageType.Thumbnail, PSPCompression.JPEG);
                    JPEGCompositeInfoChunk jpgChunk = new JPEGCompositeInfoChunk();
                    CompositeImageInfoChunk infoChunk = new CompositeImageInfoChunk();

                    using (RenderArgs args = new RenderArgs(scratchSurface))
                    {
                        input.Render(args, true);

                        int channelCount = 3;
                        if (LayerHasTransparency(args.Surface, args.Surface.Bounds))
                        {
                            channelCount = 4;
                            infoChunk.channelCount = 4;
                            infoChunk.bitmapCount = 2;
                        }
#if DEBUG
                        using (Bitmap bmp = args.Surface.CreateAliasedBitmap())
                        {

                        }
#endif

                        this.totalProgress += channelCount;

                        infoChunk.channelBlocks = SplitImageChannels(args.Surface, args.Surface.Bounds, channelCount, majorVersion, true, callback);

                        using (Surface fit = new Surface(jpegThumbSize))
                        {
                            fit.FitSurface(ResamplingAlgorithm.SuperSampling, args.Surface);

                            if (channelCount == 4)
                            {
                                unsafe
                                {
                                    for (int y = 0; y < jpgAttr.height; y++)
                                    {
                                        ColorBgra* ptr = fit.GetRowAddressUnchecked(y);
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
                    }

                    this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.Composite);
                    this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.Thumbnail);

                    if (infoChunk.bitmapCount == 2)
                    {
                        this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.CompositeTransparency);
                    }

                    this.compImage = new CompositeImageBlock(new CompositeImageAttributesChunk[2] { jpgAttr, normAttr }, jpgChunk, infoChunk);
                }
                else
                {
                    Size thumbSize = GetThumbnailDimensions(input.Width, input.Height, 300);

                    this.v5Thumbnail = new ThumbnailBlock(thumbSize.Width, thumbSize.Height);

                    scratchSurface.Clear(ColorBgra.White);
                    using (RenderArgs args = new RenderArgs(scratchSurface))
                    {
                        input.Render(args, false);
                        using (Surface fit = new Surface(thumbSize))
                        {
                            fit.FitSurface(ResamplingAlgorithm.SuperSampling, args.Surface);

                            this.totalProgress += 3;

                            this.v5Thumbnail.channelBlocks = SplitImageChannels(args.Surface, args.Surface.Bounds, 3, majorVersion, true, callback);
                        }
                    }
                }

                int layerCount = input.Layers.Count;

                LayerInfoChunk[] layerInfoChunks = new LayerInfoChunk[layerCount];
                LayerBitmapInfoChunk[] layerBitmapChunks = new LayerBitmapInfoChunk[layerCount];

                for (int i = 0; i < layerCount; i++)
                {
                    BitmapLayer layer = (BitmapLayer)input.Layers[i];

                    Rectangle savedBounds = PSPUtil.GetImageSaveRectangle(layer.Surface);

                    LayerInfoChunk infoChunk = new LayerInfoChunk(layer, BlendOptoBlendMode(layer.BlendOp), savedBounds, majorVersion);

                    int channelCount = 3;
                    int bitmapCount = 1;

                    if (LayerHasTransparency(layer.Surface, savedBounds))
                    {
                        channelCount = 4;
                        bitmapCount = 2;
                    }

                    LayerBitmapInfoChunk biChunk = new LayerBitmapInfoChunk(bitmapCount, channelCount);
                    biChunk.channels = SplitImageChannels(layer.Surface, savedBounds, channelCount, majorVersion, false, callback);

                    if (majorVersion <= PSPConstants.majorVersion5)
                    {
                        infoChunk.v5BitmapCount = biChunk.bitmapCount;
                        infoChunk.v5ChannelCount = biChunk.channelCount;
                    }

                    layerInfoChunks[i] = infoChunk;
                    layerBitmapChunks[i] = biChunk;
                }

                this.layerBlock = new LayerBlock(layerInfoChunks, layerBitmapChunks);

                string creatorData = input.Metadata.GetUserValue(PSPCreatorMetaData);
                if (!string.IsNullOrEmpty(creatorData))
                {
                    this.creator = DeserializeFromBase64<CreatorBlock>(creatorData);
                }
                else
                {
                    this.creator = new CreatorBlock();
                }

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
    }
}
