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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    internal class GeneralImageAttributes
    {
        private uint chunkSize;
        private int width;
        private int height;
        private double resValue;
        private ResolutionMetric resUnits; // byte
        private PSPCompression compressionType; // ushort
        private ushort bitDepth;
        private ushort planeCount;
        private uint colorCount;
        private byte grayScale;
        private uint totalImageSize;
        private int activeLayer;
        private ushort layerCount;
        private PSPGraphicContents graphicContents;

        public int Width
        {
            get
            {
                return this.width;
            }
        }
        public int Height
        {
            get
            {
                return this.height;
            }
        }
        public double ResValue
        {
            get
            {
                return this.resValue;
            }
            set
            {
                this.resValue = value;
            }
        }

        public ResolutionMetric ResUnit
        {
            get
            {
                return this.resUnits;
            }
            set
            {
                this.resUnits = value;
            }
        }

        public PSPCompression CompressionType
        {
            get
            {
                return this.compressionType;
            }
        }

        public ushort BitDepth
        {
            get
            {
                return this.bitDepth;
            }
        }

        public int LayerCount
        {
            get
            {
                return this.layerCount;
            }
        }

        private const uint Version6HeaderSize = 46U;
        private const uint Version5HeaderSize = 38U;

        private ushort fileMajorVersion;

        public GeneralImageAttributes(int width, int height, PSPCompression compType, int activeLayer, int layerCount, ushort majorVersion)
        {
            this.chunkSize = majorVersion > PSPConstants.majorVersion5 ? Version6HeaderSize : Version5HeaderSize;
            this.width = width;
            this.height = height;
            this.compressionType = compType;
            this.activeLayer = activeLayer;
            this.layerCount = (ushort)layerCount;
            this.bitDepth = 24;
            this.colorCount = (1U << this.bitDepth);
            this.graphicContents |= PSPGraphicContents.RasterLayers;

            this.fileMajorVersion = majorVersion;
        }

        public GeneralImageAttributes(BinaryReader br, ushort majorVersion)
        {
            this.fileMajorVersion = majorVersion;

            Load(br);
        }

        private void Load(BinaryReader br)
        {
            long dataSize = this.fileMajorVersion > PSPConstants.majorVersion5 ? Version6HeaderSize : Version5HeaderSize;

            this.chunkSize = this.fileMajorVersion > PSPConstants.majorVersion5 ? br.ReadUInt32() : 0;
            this.width = br.ReadInt32();
            this.height = br.ReadInt32();
            this.resValue = br.ReadDouble();
            this.resUnits = (ResolutionMetric)br.ReadByte();
            this.compressionType = (PSPCompression)br.ReadUInt16();
            this.bitDepth = br.ReadUInt16();
            this.planeCount = br.ReadUInt16();
            this.colorCount = br.ReadUInt32();
            this.grayScale = br.ReadByte();
            this.totalImageSize = br.ReadUInt32();
            this.activeLayer = br.ReadInt32();
            this.layerCount = br.ReadUInt16();

            if (this.fileMajorVersion > PSPConstants.majorVersion5)
            {
                this.graphicContents = (PSPGraphicContents)br.ReadUInt32();
            }
            else
            {
                this.graphicContents = (PSPGraphicContents)0U;
            }

            long dif = this.chunkSize - dataSize;

            if (dif > 0 && this.fileMajorVersion > PSPConstants.majorVersion5) // skip any unknown chunks
            {
                br.BaseStream.Position += dif;
            }
        }

        public void Save(BinaryWriter bw)
        {
            bw.Write(PSPConstants.blockIdentifier);
            bw.Write((ushort)PSPBlockID.ImageAttributes);
            if (this.fileMajorVersion > PSPConstants.majorVersion5)
            {
                bw.Write(Version6HeaderSize); // total size
                bw.Write(this.chunkSize); // total size
            }
            else
            {
                bw.Write(Version5HeaderSize); // initial size
                bw.Write(Version5HeaderSize); // total size
            }
            bw.Write(this.width);
            bw.Write(this.height);
            bw.Write(this.resValue);
            bw.Write((byte)this.resUnits);
            bw.Write((ushort)this.compressionType);
            bw.Write(this.bitDepth);
            bw.Write(this.planeCount);
            bw.Write(this.colorCount);
            bw.Write(this.grayScale);
            bw.Write(this.totalImageSize);
            bw.Write(this.activeLayer);
            bw.Write(this.layerCount);

            if (this.fileMajorVersion > PSPConstants.majorVersion5)
            {
                bw.Write((uint)this.graphicContents);
            }
        }

        public void SetGraphicContentFlag(PSPGraphicContents flag)
        {
            this.graphicContents |= flag;
        }
    }
}
