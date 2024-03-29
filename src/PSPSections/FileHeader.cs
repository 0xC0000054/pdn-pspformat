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

using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    internal class FileHeader
    {
#pragma warning disable IDE0032 // Use auto property
        private readonly ushort majorVersion;
        private readonly ushort minorVersion;
#pragma warning restore IDE0032 // Use auto property

        public ushort Major
        {
            get
            {
                return this.majorVersion;
            }
        }

        public ushort Minor
        {
            get
            {
                return this.minorVersion;
            }
        }

        public FileHeader(ushort major)
        {
            this.majorVersion = major;
            this.minorVersion = 0;
        }

        public FileHeader(IO.EndianBinaryReader br)
        {
            this.majorVersion = br.ReadUInt16();
            this.minorVersion = br.ReadUInt16();
        }

        public void Save(BinaryWriter bw)
        {
            bw.Write(this.majorVersion);
            bw.Write(this.minorVersion);
        }

    }
}
