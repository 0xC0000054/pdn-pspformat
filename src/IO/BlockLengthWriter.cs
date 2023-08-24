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

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2013 Tao Yue
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace PaintShopProFiletype.IO
{
    internal class BlockLengthWriter : IDisposable
    {
        private readonly BinaryWriter writer;
        private readonly long startPos;
        private readonly long lengthPos;
        private bool disposed;

        public BlockLengthWriter(BinaryWriter binaryWriter)
        {
            this.writer = binaryWriter;
            this.lengthPos = this.writer.BaseStream.Position;
            this.writer.Write(0xBADF00DU);
            this.startPos = this.writer.BaseStream.Position;
            this.disposed = false;
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                long end = this.writer.BaseStream.Position;
                long sectionLength = end - this.startPos;

                this.writer.BaseStream.Position = this.lengthPos;
                this.writer.Write((uint)sectionLength);

                this.writer.BaseStream.Position = end;

                this.disposed = true;
            }
        }
    }
}
