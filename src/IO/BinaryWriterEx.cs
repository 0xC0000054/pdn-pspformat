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

namespace PaintShopProFiletype.IO
{
    internal class BinaryWriterEx : BinaryWriter
    {
        private readonly bool ownStream;

        public BinaryWriterEx(Stream stream, bool ownStream) : base(stream)
        {
            this.ownStream = ownStream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.ownStream)
            {
                base.OutStream.Dispose();
            }
        }

        public void Write(System.Drawing.Rectangle rect)
        {
            Write(rect.Left);
            Write(rect.Top);
            Write(rect.Right);
            Write(rect.Bottom);
        }
    }
}
