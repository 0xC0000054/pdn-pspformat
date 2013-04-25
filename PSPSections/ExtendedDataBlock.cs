﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    class ExtendedDataBlock
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
            this.values = new Dictionary<PSPExtendedDataID,byte[]>();
            this.Load(br);
        }

        public void Load(BinaryReader br)
        {
            long pos = br.BaseStream.Position;

            long endOffset = pos + (long)blockLength;

            while ((br.BaseStream.Position < endOffset) && br.ReadUInt32() == PSPConstants.fieldIdentifier)
            {
                PSPExtendedDataID fieldID = (PSPExtendedDataID)br.ReadUInt16();

                uint fieldLength = br.ReadUInt32();

                byte[] data = br.ReadBytes((int)fieldLength);
                values.Add(fieldID, data);
            }

            long dif = this.blockLength - (br.BaseStream.Position - pos);

            if (dif > 0)
            {
                br.BaseStream.Position += dif;
            }
        }
    }
}