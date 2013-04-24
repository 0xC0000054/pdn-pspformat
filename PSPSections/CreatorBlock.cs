using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
	class CreatorBlock
	{
		private string title;
		private uint createDate;
		private uint modDate;
		private string artist;
		private string copyRight;
		private string desc;
		private PSPCreatorAppID appID;
		private uint appVersion;

		private uint blockLength;

		private static readonly DateTime unixEpochUtc = new DateTime(1970, 1, 1).ToUniversalTime();
		/// <summary>
		/// Gets the current Unix Timestamp for the createDate and modDate time stamps 
		/// </summary>
		/// <returns>The datestamp in Unix format</returns> 
		private static uint GetCurrentUnixTimestamp()
		{
			TimeSpan t = (DateTime.UtcNow - unixEpochUtc);
			
			return (uint)t.TotalSeconds;
		}

#if DEBUG
		public DateTime CreateDate
		{
			get
			{
				return unixEpochUtc.AddSeconds(createDate);
			}
		}

		public DateTime ModDate
		{ 
			get
			{
				return unixEpochUtc.AddSeconds(modDate);
			}
		}
		
#endif

		public CreatorBlock()
		{
			this.blockLength = 28; // 5 UInt32 values

			this.title = string.Empty;
			this.createDate = this.modDate = GetCurrentUnixTimestamp();
			this.artist = string.Empty;
			this.copyRight = string.Empty;
			this.desc = string.Empty;
			this.appID = PSPCreatorAppID.PSP_CREATOR_APP_UNKNOWN;
			this.appVersion = 1;
		}

		public CreatorBlock(BinaryReader br, uint blockLength)
		{
			this.blockLength = blockLength;
			this.Load(br);
		}

		private void Load(BinaryReader br)
		{
			long endOffset = br.BaseStream.Position + (long)blockLength;

			while (br.BaseStream.Position < endOffset && br.ReadUInt32() == PSPConstants.fieldIdentifier)
			{
				ushort fieldID = br.ReadUInt16();
				uint fieldLength = br.ReadUInt32();
				PSPCreatorFieldID field = (PSPCreatorFieldID)fieldID;
				switch (field)
				{
					case PSPCreatorFieldID.PSP_CRTR_FLD_TITLE:
						this.title = Encoding.ASCII.GetString(br.ReadBytes((int)fieldLength));
						break;
					case PSPCreatorFieldID.PSP_CRTR_FLD_CRT_DATE:
						this.createDate = br.ReadUInt32();
						break;
					case PSPCreatorFieldID.PSP_CRTR_FLD_MOD_DATE:
						this.modDate = br.ReadUInt32();
						break;
					case PSPCreatorFieldID.PSP_CRTR_FLD_ARTIST:
						this.artist = Encoding.ASCII.GetString(br.ReadBytes((int)fieldLength));
						break;
					case PSPCreatorFieldID.PSP_CRTR_FLD_CPYRGHT:
						this.copyRight = Encoding.ASCII.GetString(br.ReadBytes((int)fieldLength));
						break;
					case PSPCreatorFieldID.PSP_CRTR_FLD_DESC:
						this.desc = Encoding.ASCII.GetString(br.ReadBytes((int)fieldLength));
						break;
					case PSPCreatorFieldID.PSP_CRTR_FLD_APP_ID:
						this.appID = (PSPCreatorAppID)br.ReadUInt32();
						break;
					case PSPCreatorFieldID.PSP_CRTR_FLD_APP_VER:
						this.appVersion = br.ReadUInt32();
						break;
					default:
						break;
				} 
			}
		}
	}
}
