////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    class FileHeader
    {
        private ushort majorVersion;
        private ushort minorVersion;

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


        public FileHeader(BinaryReader br)
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
