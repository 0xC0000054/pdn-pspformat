﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015, 2019 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    internal class ExtendedDataBlock
    {
        private uint blockLength;
        private Dictionary<PSPExtendedDataID, byte[]> values;

        public Dictionary<PSPExtendedDataID, byte[]> Values
        {
            get
            {
                return values;
            }
        }

        public ExtendedDataBlock(BinaryReader br, uint blockLength)
        {
            this.blockLength = blockLength;
            this.values = new Dictionary<PSPExtendedDataID, byte[]>();
            this.Load(br);
        }

        private void Load(BinaryReader br)
        {
            long startOffset = br.BaseStream.Position;

            long endOffset = startOffset + (long)blockLength;

            while ((br.BaseStream.Position < endOffset) && br.ReadUInt32() == PSPConstants.fieldIdentifier)
            {
                PSPExtendedDataID fieldID = (PSPExtendedDataID)br.ReadUInt16();

                uint fieldLength = br.ReadUInt32();

                byte[] data = br.ReadBytes((int)fieldLength);
                values.Add(fieldID, data);
            }

            long dif = this.blockLength - (br.BaseStream.Position - startOffset);

            if (dif > 0)
            {
                br.BaseStream.Position += dif;
            }
        }
    }
}
