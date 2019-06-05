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
using System.IO;
using System.Text;

namespace PaintShopProFiletype.PSPSections
{
    [Serializable]
    internal sealed class CreatorBlock
    {
        private string title;
        private uint createDate;
        [NonSerialized]
        private uint modDate;
        private string artist;
        private string copyRight;
        private string description;
        [NonSerialized]
        private PSPCreatorAppID appID;
        [NonSerialized]
        private uint appVersion;

        private static readonly DateTime UnixEpochLocal = new DateTime(1970, 1, 1).ToLocalTime();
        /// <summary>
        /// Gets the current date and time in Unix format (the number of seconds from 1/1/1970)
        /// </summary>
        /// <returns>The current date and time in Unix format</returns>
        private static uint GetCurrentUnixTimestamp()
        {
            TimeSpan t = (DateTime.Now - UnixEpochLocal);

            return (uint)t.TotalSeconds;
        }

#if DEBUG
        public DateTime CreateDate
        {
            get
            {
                return UnixEpochLocal.AddSeconds(createDate);
            }
        }

        public DateTime ModDate
        {
            get
            {
                return UnixEpochLocal.AddSeconds(modDate);
            }
        }
#endif

        public CreatorBlock()
        {
            this.title = string.Empty;
            this.createDate = GetCurrentUnixTimestamp();
            this.modDate = 0U;
            this.artist = string.Empty;
            this.copyRight = string.Empty;
            this.description = string.Empty;
            this.appID = PSPCreatorAppID.Unknown;
            this.appVersion = 1;
        }

        public CreatorBlock(BinaryReader reader, uint blockLength)
        {
            long endOffset = reader.BaseStream.Position + (long)blockLength;

            while (reader.BaseStream.Position < endOffset && reader.ReadUInt32() == PSPConstants.fieldIdentifier)
            {
                PSPCreatorFieldID field = (PSPCreatorFieldID)reader.ReadUInt16();
                uint fieldLength = reader.ReadUInt32();

                switch (field)
                {
                    case PSPCreatorFieldID.Title:
                        this.title = Encoding.ASCII.GetString(reader.ReadBytes((int)fieldLength)).Trim();
                        break;
                    case PSPCreatorFieldID.CreateDate:
                        this.createDate = reader.ReadUInt32();
                        break;
                    case PSPCreatorFieldID.ModifiedDate:
                        this.modDate = reader.ReadUInt32();
                        break;
                    case PSPCreatorFieldID.Artist:
                        this.artist = Encoding.ASCII.GetString(reader.ReadBytes((int)fieldLength)).Trim();
                        break;
                    case PSPCreatorFieldID.Copyright:
                        this.copyRight = Encoding.ASCII.GetString(reader.ReadBytes((int)fieldLength)).Trim();
                        break;
                    case PSPCreatorFieldID.Description:
                        this.description = Encoding.ASCII.GetString(reader.ReadBytes((int)fieldLength)).Trim();
                        break;
                    case PSPCreatorFieldID.ApplicationID:
                        this.appID = (PSPCreatorAppID)reader.ReadUInt32();
                        break;
                    case PSPCreatorFieldID.ApplicationVersion:
                        this.appVersion = reader.ReadUInt32();
                        break;
                    default:
                        break;
                }
            }
        }

        private static void WriteASCIIField(BinaryWriter writer, PSPCreatorFieldID field, string value)
        {
            writer.Write(PSPConstants.fieldIdentifier);
            writer.Write((ushort)field);

            byte[] bytes = Encoding.ASCII.GetBytes(value);

            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(PSPConstants.blockIdentifier);
            writer.Write((ushort)PSPBlockID.Creator);

            using (new PSPUtil.BlockLengthWriter(writer))
            {
                if (!string.IsNullOrEmpty(title))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Title, this.title);
                }

                if (createDate != 0U)
                {
                    writer.Write(PSPConstants.fieldIdentifier);
                    writer.Write((ushort)PSPCreatorFieldID.CreateDate);
                    writer.Write(sizeof(uint));
                    writer.Write(this.createDate);
                }

                writer.Write(PSPConstants.fieldIdentifier);
                writer.Write((ushort)PSPCreatorFieldID.ModifiedDate);
                writer.Write(sizeof(uint));
                writer.Write(GetCurrentUnixTimestamp());

                if (!string.IsNullOrEmpty(artist))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Artist, this.artist);
                }

                if (!string.IsNullOrEmpty(copyRight))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Copyright, this.copyRight);
                }

                if (!string.IsNullOrEmpty(description))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Description, this.description);
                }
            }

        }
    }
}
