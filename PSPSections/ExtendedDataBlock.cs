////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-pspformat, a FileType plugin for Paint.NET
//
// Copyright (c) 2011-2015, 2019, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace PaintShopProFiletype.PSPSections
{
    internal class ExtendedDataBlock
    {
        private uint blockLength;
#pragma warning disable IDE0032 // Use auto property
        private Dictionary<PSPExtendedDataID, byte[]> values;
#pragma warning restore IDE0032 // Use auto property

        public Dictionary<PSPExtendedDataID, byte[]> Values
        {
            get
            {
                return this.values;
            }
        }

        public ExtendedDataBlock(BufferedBinaryReader br, uint blockLength)
        {
            this.blockLength = blockLength;
            this.values = new Dictionary<PSPExtendedDataID, byte[]>();
            Load(br);
        }

        private void Load(BufferedBinaryReader br)
        {
            long startOffset = br.Position;

            long endOffset = startOffset + this.blockLength;

            while ((br.Position < endOffset) && br.ReadUInt32() == PSPConstants.fieldIdentifier)
            {
                PSPExtendedDataID fieldID = (PSPExtendedDataID)br.ReadUInt16();

                uint fieldLength = br.ReadUInt32();

                byte[] data = br.ReadBytes((int)fieldLength);
                this.values.Add(fieldID, data);
            }

            long dif = this.blockLength - (br.Position - startOffset);

            if (dif > 0)
            {
                br.Position += dif;
            }
        }
    }
}
