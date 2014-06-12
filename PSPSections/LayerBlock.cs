using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
	struct BlendRange
	{
		public uint sourceRange;
		public uint destRange;
	}

	struct LayerBitmapInfoChunk
	{
		public uint chunkSize;
		public ushort bitmapCount;
		public ushort channelCount;
		public ChannelSubBlock[] channels;

		public LayerBitmapInfoChunk(BinaryReader br, GeneralImageAttributes attr, ushort majorVersion)
		{
			this.chunkSize = br.ReadUInt32();
			this.bitmapCount = br.ReadUInt16();
			this.channelCount = br.ReadUInt16();
			this.channels = new ChannelSubBlock[channelCount];

			for (int i = 0; i < channelCount; i++)
			{
				uint head = br.ReadUInt32();
				ushort blockID = br.ReadUInt16();
				uint size = br.ReadUInt32();

				ChannelSubBlock block = new ChannelSubBlock(br, attr.CompressionType, majorVersion);
				this.channels[i] = block;
			}
		}

		public LayerBitmapInfoChunk(BinaryReader br, GeneralImageAttributes attr, ushort v5BitmapCount, ushort v5ChannelCount)
		{
			this.chunkSize = 0;
			this.bitmapCount = v5BitmapCount;
			this.channelCount = v5ChannelCount;
			this.channels = new ChannelSubBlock[channelCount];

			for (int i = 0; i < channelCount; i++)
			{
				uint head = br.ReadUInt32();
				ushort blockID = br.ReadUInt16();
				uint initialSize = br.ReadUInt32();
				uint size = br.ReadUInt32();

				ChannelSubBlock block = new ChannelSubBlock(br, attr.CompressionType, PSPConstants.majorVersion5);
				this.channels[i] = block;
			}
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
				this.channels[i].Save(bw, majorVersion);
			}
		}
	}

	struct LayerInfoChunk
	{
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

		public uint fileMajorVersion;

		public LayerInfoChunk(BinaryReader br, ushort majorVersion)
		{
			this.fileMajorVersion = majorVersion;

			long pos = br.BaseStream.Position;
			
		   
			if (majorVersion > PSPConstants.majorVersion5)
			{
				this.chunkSize = br.ReadUInt32();
				ushort nameLen = br.ReadUInt16();
				this.name = Encoding.ASCII.GetString(br.ReadBytes(nameLen)); 
			}
			else
			{
				this.chunkSize = 0;
				this.name = Encoding.ASCII.GetString(br.ReadBytes(256)).TrimEnd(new char[] {'\0'});
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
			this.blendRanges = new BlendRange[5];
			for (int i = 0; i < 5; i++)
			{
				blendRanges[i] = new BlendRange();
				blendRanges[i].sourceRange = br.ReadUInt32();
				blendRanges[i].destRange = br.ReadUInt32();
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


			long dif = this.chunkSize - (br.BaseStream.Position - pos);
			if (dif > 0)
			{
				br.BaseStream.Position += dif;
			}
		}

		public void Save(BinaryWriterEx bw)
		{
			if (fileMajorVersion > PSPConstants.majorVersion5)
			{           
				bw.Write(this.chunkSize);
				byte[] nameBytes = Encoding.ASCII.GetBytes(this.name);
				bw.Write((ushort)nameBytes.Length);
				bw.Write(nameBytes);
			}
			else
			{
				byte[] nameBytes = new byte[256];
				if (name.Length > 255)
				{
					byte[] temp = Encoding.ASCII.GetBytes(name.Substring(0, 255));
					Buffer.BlockCopy(temp, 0, nameBytes, 0, 255);
					nameBytes[255] = 0;
				}
				else
				{
					byte[] temp = Encoding.ASCII.GetBytes(name + "\0");
					Buffer.BlockCopy(temp, 0, nameBytes, 0, temp.Length);
				}

				bw.Write(nameBytes);
			}
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
			for (int i = 0; i < 5; i++)
			{
				bw.Write(0U);
				bw.Write(0U);
			}
			if (fileMajorVersion <= PSPConstants.majorVersion5)
			{
				bw.Write(this.v5BitmapCount);
				bw.Write(this.v5ChannelCount);
			}
		}
	}

	class LayerBlock
	{
		private LayerInfoChunk[] layerInfoChunks;
		private LayerBitmapInfoChunk[] layerBitmapInfo;
		private GeneralImageAttributes imageAttributes;
		private ushort fileMajorVersion;

		public LayerInfoChunk[] LayerInfo
		{
			get
			{
				return layerInfoChunks;
			}
		}

		public LayerBitmapInfoChunk[] LayerBitmapInfo
		{ 
			get
			{
				return layerBitmapInfo;
			}
		}

		public LayerBlock(LayerInfoChunk[] infoChunks, LayerBitmapInfoChunk[] biChunks)
		{
			this.layerInfoChunks = infoChunks;
			this.layerBitmapInfo = biChunks;
		}

		public LayerBlock(BinaryReader br, GeneralImageAttributes attr, ushort majorVersion)
		{
			this.fileMajorVersion = majorVersion;
			this.imageAttributes = attr;

			this.Load(br);
		}

		private void Load(BinaryReader br)
		{
			RasterLayerInfo raster = CountRasterChunks(br);
			
			int layerCount = raster.count;

			if (layerCount == 0)
			{
				throw new FormatException(Properties.Resources.RasterLayerNotFound);
			}

			this.layerInfoChunks = new LayerInfoChunk[layerCount];
			this.layerBitmapInfo = new LayerBitmapInfoChunk[layerCount];

			for (int i = 0; i < layerCount; i++)
			{
				br.BaseStream.Seek(raster.infoChunkOffsets[i], SeekOrigin.Begin);

				LayerInfoChunk chunk = new LayerInfoChunk(br, fileMajorVersion); 
				this.layerInfoChunks[i] = chunk;

				if (!chunk.saveRect.IsEmpty)
				{
					if (fileMajorVersion <= PSPConstants.majorVersion5)
					{
						LayerBitmapInfoChunk biChunk = new LayerBitmapInfoChunk(br, imageAttributes, chunk.v5BitmapCount, chunk.v5ChannelCount);

						this.layerBitmapInfo[i] = biChunk;
					}
					else
					{
						LayerBitmapInfoChunk biChunk = new LayerBitmapInfoChunk(br, imageAttributes, fileMajorVersion);

						this.layerBitmapInfo[i] = biChunk;
					} 
				} 
			}
		}

		private struct RasterLayerInfo
		{
			public int count;
			public long[] infoChunkOffsets;
		} 

		private RasterLayerInfo CountRasterChunks(BinaryReader reader)
		{
			int layerCount = imageAttributes.LayerCount;
			
			int rasterCount = 0;
			List<long> rasterInfoOffsets = new List<long>(layerCount);

			int index = 0;
			while (index < layerCount)
			{
				uint head = reader.ReadUInt32();
				PSPBlockID blockID = (PSPBlockID)reader.ReadUInt16();
				uint initialBlockLength = fileMajorVersion <= PSPConstants.majorVersion5 ? reader.ReadUInt32() : 0;
				uint blockLength = reader.ReadUInt32();

				if (blockID == PSPBlockID.Layer)
				{
					index++;
					long startOffset = reader.BaseStream.Position;
					long endOffset = startOffset + (long)blockLength;

					LayerInfoChunk chunk = new LayerInfoChunk(reader, fileMajorVersion);

					switch (chunk.type)
					{
						case PSPLayerType.Raster:
						case PSPLayerType.FloatingRasterSelection:
							rasterCount++;
							rasterInfoOffsets.Add(startOffset);
							break;
					}

					reader.BaseStream.Position += (endOffset - reader.BaseStream.Position);         
				}
				else
				{
					reader.BaseStream.Position += (long)blockLength;
				}  
			}

			return new RasterLayerInfo() { count = rasterCount, infoChunkOffsets = rasterInfoOffsets.ToArray() };
		}

		public void Save(BinaryWriterEx bw, ushort majorVersion)
		{
			bw.Write(PSPConstants.blockIdentifier);
			bw.Write((ushort)PSPBlockID.LayerStart);
			if (majorVersion <= PSPConstants.majorVersion5)
			{
				bw.Write(0U); // length of first LayerInfoChunk
			}
			
			using (new PSPUtil.BlockLengthWriter(bw))
			{
				int count = this.layerBitmapInfo.Length;

				for (int i = 0; i < count; i++)
				{
					bw.Write(PSPConstants.blockIdentifier);
					bw.Write((ushort)PSPBlockID.Layer);
					if (majorVersion <= PSPConstants.majorVersion5)
					{
						bw.Write(375U); // size of the first layer info chunk
					}

					using (new PSPUtil.BlockLengthWriter(bw))
					{
						this.layerInfoChunks[i].Save(bw);
						if (!layerInfoChunks[i].saveRect.IsEmpty)
						{
							this.layerBitmapInfo[i].Save(bw, majorVersion); 
						}
					}
				} 
			}

		}
	}
}
