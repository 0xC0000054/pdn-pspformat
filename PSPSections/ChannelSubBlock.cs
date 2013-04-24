using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Ionic.Zlib;

namespace PaintShopProFiletype.PSPSections
{
	struct ChannelSubBlock
	{
		public uint chunkSize;
		public uint compressedChannelLength;
		public uint uncompressedChannelLength;
		public PSPDIBType bitmapType;
		public PSPChannelType channelType;
		public byte[] channelData;

		public ChannelSubBlock(BinaryReader br, PSPCompression compressionType, ushort majorVersion)
		{
			this.chunkSize = majorVersion > PSPConstants.majorVersion5 ? br.ReadUInt32() : 0U;
			this.compressedChannelLength = br.ReadUInt32();
			this.uncompressedChannelLength = br.ReadUInt32();
			this.bitmapType = (PSPDIBType)br.ReadUInt16();
			this.channelType = (PSPChannelType)br.ReadUInt16();
			this.channelData = null;
  
			long dif = chunkSize - 16U;
			if (dif > 0 && majorVersion > PSPConstants.majorVersion5)
			{
				br.BaseStream.Position += dif;
			}

			if (compressedChannelLength > 0U)
			{
				switch (compressionType)
				{
					case PSPCompression.PSP_COMP_NONE:
						this.channelData = br.ReadBytes((int)compressedChannelLength);
						break;
					case PSPCompression.PSP_COMP_RLE:
						this.channelData = RLE.Decompress(br.ReadBytes((int)compressedChannelLength), uncompressedChannelLength);
						break;
					case PSPCompression.PSP_COMP_LZ77:

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
			bw.Write((ushort)PSPBlockID.PSP_CHANNEL_BLOCK);
			if (majorVersion > PSPConstants.majorVersion5)
			{
				bw.Write(16U + this.compressedChannelLength);            
				bw.Write(this.chunkSize);
			}
			else
			{
				bw.Write(12U); // initial size
				bw.Write(12U + this.compressedChannelLength);
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
