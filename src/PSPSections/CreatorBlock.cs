﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015, 2019, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PaintShopProFiletype.PSPSections
{
    [Serializable]
    [DebuggerTypeProxy(typeof(DebugView))]
    internal sealed partial class CreatorBlock
    {
        private static readonly DateTime UnixEpochLocal = new DateTime(1970, 1, 1).ToLocalTime();

        // These field are required for compatibility with binary serialization.
#pragma warning disable IDE0044 // Add readonly modifier
        private string title;
        private uint createDate;
        [NonSerialized]
        private uint modDate;
        private string artist;
        private string copyRight;
        private string description;
#pragma warning restore IDE0044 // Add readonly modifier

        public CreatorBlock()
        {
            this.title = string.Empty;
            this.createDate = GetCurrentUnixTimestamp();
            this.modDate = 0U;
            this.artist = string.Empty;
            this.copyRight = string.Empty;
            this.description = string.Empty;
        }

        public CreatorBlock(BufferedBinaryReader reader, uint blockLength)
        {
            long endOffset = reader.Position + blockLength;

            while (reader.Position < endOffset && reader.ReadUInt32() == PSPConstants.fieldIdentifier)
            {
                PSPCreatorFieldID field = (PSPCreatorFieldID)reader.ReadUInt16();
                uint fieldLength = reader.ReadUInt32();

                switch (field)
                {
                    case PSPCreatorFieldID.Title:
                        this.title = reader.ReadAsciiString((int)fieldLength).Trim();
                        break;
                    case PSPCreatorFieldID.CreateDate:
                        this.createDate = reader.ReadUInt32();
                        break;
                    case PSPCreatorFieldID.ModifiedDate:
                        this.modDate = reader.ReadUInt32();
                        break;
                    case PSPCreatorFieldID.Artist:
                        this.artist = reader.ReadAsciiString((int)fieldLength).Trim();
                        break;
                    case PSPCreatorFieldID.Copyright:
                        this.copyRight = reader.ReadAsciiString((int)fieldLength).Trim();
                        break;
                    case PSPCreatorFieldID.Description:
                        this.description = reader.ReadAsciiString((int)fieldLength).Trim();
                        break;
#if DEBUG
                    case PSPCreatorFieldID.ApplicationID:
                        PSPCreatorAppID appID = (PSPCreatorAppID)reader.ReadUInt32();
                        break;
                    case PSPCreatorFieldID.ApplicationVersion:
                        uint appVersion = reader.ReadUInt32();
                        break;
#endif
                    default:
                        reader.Position += fieldLength;
                        break;
                }
            }
        }

        public string Title => this.title;

        public uint CreateDate => this.createDate;

        public uint ModDate => this.modDate;

        public string Artist => this.artist;

        public string CopyRight => this.copyRight;

        public string Description => this.description;

        public void Save(BinaryWriter writer)
        {
            writer.Write(PSPConstants.blockIdentifier);
            writer.Write((ushort)PSPBlockID.Creator);

            using (new PSPUtil.BlockLengthWriter(writer))
            {
                if (!string.IsNullOrEmpty(this.title))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Title, this.title);
                }

                if (this.createDate != 0U)
                {
                    WriteUInt32Field(writer, PSPCreatorFieldID.CreateDate, this.createDate);
                }

                WriteUInt32Field(writer, PSPCreatorFieldID.ModifiedDate, GetCurrentUnixTimestamp());

                if (!string.IsNullOrEmpty(this.artist))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Artist, this.artist);
                }

                if (!string.IsNullOrEmpty(this.copyRight))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Copyright, this.copyRight);
                }

                if (!string.IsNullOrEmpty(this.description))
                {
                    WriteASCIIField(writer, PSPCreatorFieldID.Description, this.description);
                }
            }
        }

        /// <summary>
        /// Gets the current date and time in Unix format (the number of seconds from 1/1/1970)
        /// </summary>
        /// <returns>The current date and time in Unix format</returns>
        private static uint GetCurrentUnixTimestamp()
        {
            TimeSpan t = (DateTime.Now - UnixEpochLocal);

            return (uint)t.TotalSeconds;
        }

        private static void WriteASCIIField(BinaryWriter writer, PSPCreatorFieldID field, string value)
        {
            writer.Write(PSPConstants.fieldIdentifier);
            writer.Write((ushort)field);

            byte[] bytes = Encoding.ASCII.GetBytes(value);

            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteUInt32Field(BinaryWriter writer, PSPCreatorFieldID field, uint value)
        {
            writer.Write(PSPConstants.fieldIdentifier);
            writer.Write((ushort)field);
            writer.Write(sizeof(uint));
            writer.Write(value);
        }
    }
}
