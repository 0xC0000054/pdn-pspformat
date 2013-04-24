using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
	class GeneralImageAttributes
	{
		private uint chunkSize;
		private int width;
		private int height;
		private double resValue;
		private PSP_METRIC resUnits; // byte
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
				return width;
			}
		}
		public int Height
		{
			get
			{
				return height;
			}
		}
		public double ResValue
		{
			get 
			{
				return resValue;
			}
			set 
			{
				resValue = value;
			}
		}

		public PSP_METRIC ResUnit
		{
			get
			{
				return resUnits;
			}
			set
			{
				resUnits = value;
			}
		}

		public PSPCompression CompressionType
		{
			get 
			{
				return compressionType;
			}
		}

		public ushort BitDepth
		{ 
			get
			{
				return bitDepth;
			}
		}

		public int LayerCount
		{
			get
			{
				return layerCount;
			}
		}

		private ushort fileMajorVersion;

		public GeneralImageAttributes(int width, int height, PSPCompression compType, int activeLayer, int layerCount, ushort majorVersion)
		{
			this.chunkSize = majorVersion > PSPConstants.majorVersion5 ? 46U : 38U;
			this.width = width;
			this.height = height;
			this.compressionType = compType;
			this.activeLayer = activeLayer;
			this.layerCount = (ushort)layerCount;
			this.bitDepth = 24;
			this.colorCount = (1U << this.bitDepth);
			this.graphicContents |= PSPGraphicContents.keGCRasterLayers;

			this.fileMajorVersion = majorVersion;
		}

		public GeneralImageAttributes(BinaryReader br, ushort majorVersion)
		{
			this.fileMajorVersion = majorVersion;

			this.Load(br);
		}

		private void Load(BinaryReader br)
		{
			int dataSize = this.fileMajorVersion > PSPConstants.majorVersion5 ? 46 : 38;

			this.chunkSize = this.fileMajorVersion > PSPConstants.majorVersion5 ? br.ReadUInt32() : 0;
			this.width = br.ReadInt32();
			this.height = br.ReadInt32();
			this.resValue = br.ReadDouble();
			this.resUnits = (PSP_METRIC)br.ReadByte();
			this.compressionType = (PSPCompression)br.ReadUInt16();
			this.bitDepth = br.ReadUInt16();
			this.planeCount = br.ReadUInt16();
			this.colorCount = br.ReadUInt32();
			this.grayScale = br.ReadByte();
			this.totalImageSize = br.ReadUInt32();
			this.activeLayer = br.ReadInt32();
			this.layerCount = br.ReadUInt16();

			if (fileMajorVersion > PSPConstants.majorVersion5)
			{
				this.graphicContents = (PSPGraphicContents)br.ReadUInt32();
			}
			else
			{
				this.graphicContents = (PSPGraphicContents)0U;
			}

			long dif = chunkSize - dataSize;

			if (dif > 0 && fileMajorVersion > PSPConstants.majorVersion5) // skip any unknown chunks
			{
				br.BaseStream.Position += dif;
			}
		}

		public void Save(BinaryWriter bw)
		{
			bw.Write(PSPConstants.blockIdentifier);
			bw.Write((ushort)PSPBlockID.PSP_IMAGE_BLOCK);
			if (this.fileMajorVersion > PSPConstants.majorVersion5)
			{
				bw.Write(46U); // total size                
				bw.Write(this.chunkSize); // total size
			}
			else
			{
				bw.Write(38U); // initial size
				bw.Write(38U); // total size
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
