﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace PaintShopProFiletype.PSPSections
{
	struct CompositeImageAttributesChunk
	{
		public uint chunkSize;
		public int width;
		public int height;  
		public ushort bitDepth;
		public PSPCompression compressionType; // ushort
		public ushort planeCount;
		public uint colorCount;
		public PSPCompositeImageType compositeImageType; // ushort

#if DEBUG
		public CompositeImageAttributesChunk(BinaryReader br)
		{
			uint dataSize = 24;

			this.chunkSize = br.ReadUInt32();
			this.width = br.ReadInt32();
			this.height = br.ReadInt32();
			this.bitDepth = br.ReadUInt16();
			this.compressionType = (PSPCompression)br.ReadUInt16();
			this.planeCount = br.ReadUInt16();
			this.colorCount = br.ReadUInt32();
			this.compositeImageType = (PSPCompositeImageType)br.ReadUInt16();

			uint dif = chunkSize - dataSize;
			if (dif > 0)
			{
				br.BaseStream.Position += (long)dif;
			}

		} 
#endif

		public void Save(BinaryWriter bw)
		{
			bw.Write(PSPConstants.blockIdentifier);
			bw.Write((ushort)PSPBlockID.PSP_COMPOSITE_ATTRIBUTES_BLOCK);
			bw.Write(24U);
			bw.Write(this.chunkSize);
			bw.Write(this.width);
			bw.Write(this.height);
			bw.Write(this.bitDepth);
			bw.Write((ushort)this.compressionType);
			bw.Write(this.planeCount);
			bw.Write(this.colorCount);
			bw.Write((ushort)compositeImageType);
		}
	}

	struct JPEGCompositeInfoChunk
	{
		public uint chunkSize;
		public uint compressedSize;
		public uint unCompressedSize;
		public PSPDIBType imageType;

		public byte[] imageData;

#if DEBUG
		public JPEGCompositeInfoChunk(BinaryReader br)
		{
			uint dataSize = 14;
			this.chunkSize = br.ReadUInt32();
			this.compressedSize = br.ReadUInt32();
			this.unCompressedSize = br.ReadUInt32();
			this.imageType = (PSPDIBType)br.ReadUInt16();

			uint dif = chunkSize - dataSize;
			if (dif > 0)
			{
				br.BaseStream.Position += (long)dif;
			}

			this.imageData = br.ReadBytes((int)this.compressedSize);
		} 
#endif

		public void Save(BinaryWriter bw)
		{
			bw.Write(PSPConstants.blockIdentifier);
			bw.Write((ushort)PSPBlockID.PSP_JPEG_BLOCK);
			bw.Write(14U + this.compressedSize);
			bw.Write(this.chunkSize);
			bw.Write(this.compressedSize);
			bw.Write(this.unCompressedSize);
			bw.Write((ushort)this.imageType);
			bw.Write(this.imageData);
		}
	}

	struct CompositeImageInfoChunk
	{
		public uint chunkSize;
		public ushort bitmapCount;
		public ushort channelCount;
		public ColorPaletteBlock? paletteSubBlock;
		public ChannelSubBlock[] channelBlocks;

#if DEBUG
		public CompositeImageInfoChunk(BinaryReader br, CompositeImageAttributesChunk attr, ushort majorVersion)
		{
			this.chunkSize = br.ReadUInt32();
			this.bitmapCount = br.ReadUInt16();
			this.channelCount = br.ReadUInt16();
			this.paletteSubBlock = null;
			this.channelBlocks = new ChannelSubBlock[channelCount];

			uint dif = chunkSize - 8U;
			if (dif > 0)
			{
				br.BaseStream.Position += (long)dif;
			}

			for (int i = 0; i < channelCount; i++)
			{
				uint blockSig = br.ReadUInt32();
				PSPBlockID blockType = (PSPBlockID)br.ReadUInt16();
				uint chunkLength = br.ReadUInt32();

				switch (blockType)
				{
					case PSPBlockID.PSP_COLOR_BLOCK:
						this.paletteSubBlock = new ColorPaletteBlock(br, majorVersion);
						i--; // back up and read the channel
						break;
					case PSPBlockID.PSP_CHANNEL_BLOCK:
						ChannelSubBlock block = new ChannelSubBlock(br, attr.compressionType, majorVersion);
						channelBlocks[i] = block;
						break;
				}
			}


		} 
#endif

		public void Save(BinaryWriter writer, ushort majorVersion)
		{
			writer.Write(PSPConstants.blockIdentifier);
			writer.Write((ushort)PSPBlockID.PSP_COMPOSITE_IMAGE_BLOCK);

			using (new PSPUtil.BlockLengthWriter(writer))
			{
				writer.Write(this.chunkSize);
				writer.Write(this.bitmapCount);
				writer.Write(this.channelCount);

				for (int i = 0; i < this.channelCount; i++)
				{
					this.channelBlocks[i].Save(writer, majorVersion);
				} 
			}
		}
	   
	}

	class CompositeImageBlock
	{
		private uint blockSize;
		private uint attrChunkCount;
		private CompositeImageAttributesChunk[] attrChunks;
		private JPEGCompositeInfoChunk jpegChunk;
		private CompositeImageInfoChunk imageChunk;


		public CompositeImageBlock(CompositeImageAttributesChunk[] attributes, JPEGCompositeInfoChunk jpg, CompositeImageInfoChunk info)
		{
			this.blockSize = 8U;
			this.attrChunkCount = (uint)attributes.Length;
			this.attrChunks = attributes;
			this.jpegChunk = jpg;
			this.imageChunk = info;
		}

#if DEBUG
		public CompositeImageBlock(BinaryReader br, ushort majorVersion)
		{
			this.Load(br, majorVersion);
		}

		public void Load(BinaryReader br, ushort majorVersion)
		{
			this.blockSize = br.ReadUInt32();
			this.attrChunkCount = br.ReadUInt32();

			if ((this.blockSize - 8U) > 0)
			{
				br.BaseStream.Position += (long)(this.blockSize - 8U);
			}

			this.attrChunks = new CompositeImageAttributesChunk[(int)this.attrChunkCount];

			for (int i = 0; i < attrChunkCount; i++)
			{         
				uint blockSig = br.ReadUInt32();
				ushort blockType = br.ReadUInt16();
				uint attrChunkLength = br.ReadUInt32();

				CompositeImageAttributesChunk chunk = new CompositeImageAttributesChunk(br);
				this.attrChunks[i] = chunk;
			}

			for (int i = 0; i < attrChunkCount; i++)
			{
				uint blockSig = br.ReadUInt32();
				PSPBlockID blockType = (PSPBlockID)br.ReadUInt16();
				uint chunkLength = br.ReadUInt32();

				switch (blockType)
				{
					case PSPBlockID.PSP_COMPOSITE_IMAGE_BLOCK:
						this.imageChunk = new CompositeImageInfoChunk(br, this.attrChunks[i], majorVersion);
						break;
					case PSPBlockID.PSP_JPEG_BLOCK:
						this.jpegChunk = new JPEGCompositeInfoChunk(br);
						break;
				}
			}

			if (this.jpegChunk.imageData != null)
			{
				using (MemoryStream ms = new MemoryStream(this.jpegChunk.imageData))
				{
					using (System.Drawing.Bitmap th = new System.Drawing.Bitmap(ms))
					{
						Debug.WriteLine(string.Format("JPEG thumbnail size: {0}x{1}", th.Width, th.Height));
					}
				}
			} 
		}

#endif


		/// <summary>
		/// Saves the CompositeImageBlock to the file.
		/// </summary>
		/// <param name="bw">The BinaryWriter to write to.</param>
		public void Save(BinaryWriter bw)
		{
			bw.Write(PSPConstants.blockIdentifier);
			bw.Write((ushort)PSPBlockID.PSP_COMPOSITE_IMAGE_BANK_BLOCK);
			
			using (new PSPUtil.BlockLengthWriter(bw))
			{
				bw.Write(this.blockSize);
				bw.Write(this.attrChunkCount);

				foreach (var item in attrChunks)
				{
					item.Save(bw);
				}

				this.jpegChunk.Save(bw);
				this.imageChunk.Save(bw, PSPConstants.majorVersion6); 
			}
		}

	}
}