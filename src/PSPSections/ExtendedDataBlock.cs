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

using PaintShopProFiletype.IO;
using System;
using System.Collections.Generic;

namespace PaintShopProFiletype.PSPSections
{
    internal class ExtendedDataBlock
    {
        private readonly uint blockLength;
        private readonly List<KeyValuePair<PSPExtendedDataID, byte[]>> values;

        public ExtendedDataBlock(EndianBinaryReader br, uint blockLength)
        {
            this.blockLength = blockLength;
            this.values = new List<KeyValuePair<PSPExtendedDataID, byte[]>>();
            Load(br);
        }

        public short TryGetTransparencyIndex()
        {
            short index = -1;

            foreach (var item in this.values)
            {
                if (item.Key == PSPExtendedDataID.TransparencyIndex)
                {
                    index = BitConverter.ToInt16(item.Value, 0);
                    break;
                }
            }

            return index;
        }

        private void Load(EndianBinaryReader br)
        {
            long startOffset = br.Position;

            long endOffset = startOffset + this.blockLength;

            while ((br.Position < endOffset) && br.ReadUInt32() == PSPConstants.fieldIdentifier)
            {
                PSPExtendedDataID fieldID = (PSPExtendedDataID)br.ReadUInt16();

                uint fieldLength = br.ReadUInt32();

                byte[] data = br.ReadBytes((int)fieldLength);

                this.values.Add(new KeyValuePair<PSPExtendedDataID, byte[]>(fieldID, data));
            }

            long bytesToSkip = this.blockLength - (br.Position - startOffset);

            if (bytesToSkip > 0)
            {
                br.Position += bytesToSkip;
            }
        }
    }
}
