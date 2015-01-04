using System;
using System.Diagnostics;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
	internal sealed class CompositeImageAttributesChunk
	{
		public uint chunkSize;
		public int width;
		public int height;  
		public ushort bitDepth;
		public PSPCompression compressionType; // ushort
		public ushort planeCount;
		public uint colorCount;
		public PSPCompositeImageType compositeImageType; // ushort

		private const uint HeaderSize = 24U;

#if DEBUG
		public CompositeImageAttributesChunk(BinaryReader br)
		{
			this.chunkSize = br.ReadUInt32();
			this.width = br.ReadInt32();
			this.height = br.ReadInt32();
			this.bitDepth = br.ReadUInt16();
			this.compressionType = (PSPCompression)br.ReadUInt16();
			this.planeCount = br.ReadUInt16();
			this.colorCount = br.ReadUInt32();
			this.compositeImageType = (PSPCompositeImageType)br.ReadUInt16();

			uint dif = chunkSize - HeaderSize;
			if (dif > 0)
			{
				br.BaseStream.Position += (long)dif;
			}

		} 
#endif
		public CompositeImageAttributesChunk(int width, int height, PSPCompositeImageType imageType, PSPCompression compression)
		{
			this.chunkSize = HeaderSize;
			this.width = width;
			this.height = height;
			this.bitDepth = 24;
			this.compressionType = compression;
			this.planeCount = 1;
			this.colorCount = (1 << 24);
			this.compositeImageType = imageType;
		}

		public void Save(BinaryWriter bw)
		{
			bw.Write(PSPConstants.blockIdentifier);
			bw.Write((ushort)PSPBlockID.CompositeImageAttributes);
			bw.Write(HeaderSize);
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

	internal sealed class JPEGCompositeInfoChunk
	{
		public uint chunkSize;
		public uint compressedSize;
		public uint unCompressedSize;
		public PSPDIBType imageType;

		public byte[] imageData;

		private const uint HeaderSize = 14U;

#if DEBUG
		public JPEGCompositeInfoChunk(BinaryReader br)
		{
			this.chunkSize = br.ReadUInt32();
			this.compressedSize = br.ReadUInt32();
			this.unCompressedSize = br.ReadUInt32();
			this.imageType = (PSPDIBType)br.ReadUInt16();

			uint dif = chunkSize - HeaderSize;
			if (dif > 0)
			{
				br.BaseStream.Position += (long)dif;
			}

			this.imageData = br.ReadBytes((int)this.compressedSize);
		} 
#endif
		public JPEGCompositeInfoChunk()
		{
			this.chunkSize = HeaderSize;
			this.unCompressedSize = 0;
			this.imageType = PSPDIBType.Thumbnail;
			this.imageData = null;
		}


		public void Save(BinaryWriter bw)
		{
			bw.Write(PSPConstants.blockIdentifier);
			bw.Write((ushort)PSPBlockID.JPEGImage);
			bw.Write(HeaderSize + this.compressedSize);
			bw.Write(this.chunkSize);
			bw.Write(this.compressedSize);
			bw.Write(this.unCompressedSize);
			bw.Write((ushort)this.imageType);
			bw.Write(this.imageData);
		}
	}

	internal sealed class CompositeImageInfoChunk
	{
		public uint chunkSize;
		public ushort bitmapCount;
		public ushort channelCount;
		public ColorPaletteBlock paletteSubBlock;
		public ChannelSubBlock[] channelBlocks;

		private const uint HeaderSize = 8U;

#if DEBUG
		public CompositeImageInfoChunk(BinaryReader br, CompositeImageAttributesChunk attr, ushort majorVersion)
		{
			this.chunkSize = br.ReadUInt32();
			this.bitmapCount = br.ReadUInt16();
			this.channelCount = br.ReadUInt16();
			this.paletteSubBlock = null;
			this.channelBlocks = new ChannelSubBlock[channelCount];

			uint dif = chunkSize - HeaderSize;
			if (dif > 0)
			{
				br.BaseStream.Position += (long)dif;
			}

			int index = 0;
			do
			{
				uint blockSig = br.ReadUInt32();
				if (blockSig != PSPConstants.blockIdentifier)
				{
					throw new FormatException(Properties.Resources.InvalidBlockSignature);
				}
				PSPBlockID blockType = (PSPBlockID)br.ReadUInt16();
				uint chunkLength = br.ReadUInt32();

				switch (blockType)
				{
					case PSPBlockID.ColorPalette:
						this.paletteSubBlock = new ColorPaletteBlock(br, majorVersion);
						break;
					case PSPBlockID.Channel:
						ChannelSubBlock block = new ChannelSubBlock(br, attr.compressionType, majorVersion);
						channelBlocks[index] = block;
						index++;
						break;
				}
			}
			while (index < channelCount);
		}
#endif       
		public CompositeImageInfoChunk()
		{
			this.chunkSize = HeaderSize;
			this.bitmapCount = 1;
			this.channelCount = 3;
			this.paletteSubBlock = null;
			this.channelBlocks = null;
		}

		public void Save(BinaryWriter writer, ushort majorVersion)
		{
			writer.Write(PSPConstants.blockIdentifier);
			writer.Write((ushort)PSPBlockID.CompositeImage);

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

		private const uint HeaderSize = 8U;

		public CompositeImageBlock(CompositeImageAttributesChunk[] attributes, JPEGCompositeInfoChunk jpg, CompositeImageInfoChunk info)
		{
			this.blockSize = HeaderSize;
			this.attrChunkCount = (uint)attributes.Length;
			this.attrChunks = attributes;
			this.jpegChunk = jpg;
			this.imageChunk = info;
		}

#if DEBUG
		public CompositeImageBlock(BinaryReader br, ushort majorVersion)
		{
			this.blockSize = br.ReadUInt32();
			this.attrChunkCount = br.ReadUInt32();

            long dif = this.blockSize - HeaderSize;
			if (dif > 0)
			{
				br.BaseStream.Position += dif;
			}

			this.attrChunks = new CompositeImageAttributesChunk[(int)this.attrChunkCount];

			for (int i = 0; i < attrChunkCount; i++)
			{
				uint blockSig = br.ReadUInt32();
				if (blockSig != PSPConstants.blockIdentifier)
				{
					throw new FormatException(Properties.Resources.InvalidBlockSignature);
				}
				ushort blockType = br.ReadUInt16();
				PSPUtil.CheckBlockType(blockType, PSPBlockID.CompositeImageAttributes);
				uint attrChunkLength = br.ReadUInt32();

				this.attrChunks[i] = new CompositeImageAttributesChunk(br);
			}

			for (int i = 0; i < attrChunkCount; i++)
			{
				uint blockSig = br.ReadUInt32();
				if (blockSig != PSPConstants.blockIdentifier)
				{
					throw new FormatException(Properties.Resources.InvalidBlockSignature);
				}
				PSPBlockID blockType = (PSPBlockID)br.ReadUInt16();
				uint chunkLength = br.ReadUInt32();

				switch (blockType)
				{
					case PSPBlockID.CompositeImage:
						this.imageChunk = new CompositeImageInfoChunk(br, this.attrChunks[i], majorVersion);
						break;
					case PSPBlockID.JPEGImage:
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
			bw.Write((ushort)PSPBlockID.CompositeImageBank);
			
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
