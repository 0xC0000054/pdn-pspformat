﻿using System.IO;
using Ionic.Zlib;

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

		public ChannelSubBlock(BinaryReader br, PSPCompression compressionType, ushort majorVersion)
		{
			this.chunkSize = majorVersion > PSPConstants.majorVersion5 ? br.ReadUInt32() : 0U;
			this.compressedChannelLength = br.ReadUInt32();
			this.uncompressedChannelLength = br.ReadUInt32();
			this.bitmapType = (PSPDIBType)br.ReadUInt16();
			this.channelType = (PSPChannelType)br.ReadUInt16();
			this.channelData = null;
  
			long dif = chunkSize - Version6HeaderSize;
			if (dif > 0 && majorVersion > PSPConstants.majorVersion5)
			{
				br.BaseStream.Position += dif;
			}

			if (compressedChannelLength > 0U)
			{
				switch (compressionType)
				{
					case PSPCompression.None:
						this.channelData = br.ReadBytes((int)compressedChannelLength);
						break;
					case PSPCompression.RLE:
						this.channelData = RLE.Decompress(br.ReadBytes((int)compressedChannelLength), uncompressedChannelLength);
						break;
					case PSPCompression.LZ77:

						byte[] compressedData = br.ReadBytes((int)compressedChannelLength);
						this.channelData = new byte[uncompressedChannelLength];

						ZlibCodec codec = new ZlibCodec();
						codec.AvailableBytesIn = (int)compressedChannelLength;
						codec.AvailableBytesOut = (int)uncompressedChannelLength;
						codec.InputBuffer = compressedData;
						codec.OutputBuffer = this.channelData;
						codec.InitializeInflate();

						int rs = codec.Inflate(FlushType.Finish);
						
						codec.EndInflate();

#if DEBUG
						System.Diagnostics.Debug.Assert(rs == ZlibConstants.Z_OK || rs == ZlibConstants.Z_STREAM_END);
#else
						if (rs != ZlibConstants.Z_OK && rs != ZlibConstants.Z_STREAM_END)
						{
							throw new ZlibException(codec.Message);
						}
#endif

						break;
				} 
			}
		  

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

			if (channelData != null)
			{
				bw.Write(this.channelData);
			}
		}
	}
}
