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
using System.Drawing;
using System.Globalization;
using System.IO;
using PaintDotNet;

namespace PaintShopProFiletype.PSPSections
{
    internal static class PSPUtil
    {
        internal class BlockLengthWriter : IDisposable
        {
            private BinaryWriter writer;
            private long startPos;
            private long lengthPos;
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

        [System.Diagnostics.DebuggerDisplay("Top = {Top}, Bottom = {Bottom}, Left = {Left}, Right = {Right}")]
        private struct RectanglePosition
        {
          public int Top { get; set; }
          public int Bottom { get; set; }
          public int Left { get; set; }
          public int Right { get; set; }
        }

        public static unsafe Rectangle GetImageSaveRectangle(Surface surface)
        {
            RectanglePosition rectPos = new RectanglePosition()
            {
                Left = surface.Width,
                Top = surface.Height,
                Right = 0,
                Bottom = 0
            };

            // Search for top non-transparent pixel
            bool fPixelFound = false;
            for (int y = 0; y < surface.Height; y++)
            {
                if (ExpandImageRectangle(surface, y, 0, surface.Width, ref rectPos))
                {
                    fPixelFound = true;
                    break;
                }
            }

            // Narrow down the other dimensions of the image rectangle
            if (fPixelFound)
            {
                // Search for bottom non-transparent pixel
                for (int y = surface.Height - 1; y > rectPos.Bottom; y--)
                {
                    if (ExpandImageRectangle(surface, y, 0, surface.Width, ref rectPos))
                        break;
                }

                // Search for left and right non-transparent pixels.  Because we
                // scan horizontally, we can't just break, but we can examine fewer
                // candidate pixels on the remaining rows.
                for (int y = rectPos.Top + 1; y < rectPos.Bottom; y++)
                {
                    ExpandImageRectangle(surface, y, 0, rectPos.Left, ref rectPos);
                    ExpandImageRectangle(surface, y, rectPos.Right + 1, surface.Width, ref rectPos);
                }
            }
            else
            {
                rectPos.Left = 0;
                rectPos.Top = 0;
            }

            if ((rectPos.Left < rectPos.Right) && (rectPos.Top < rectPos.Bottom))
            {
                return new Rectangle(rectPos.Left, rectPos.Top, rectPos.Right - rectPos.Left + 1, rectPos.Bottom - rectPos.Top + 1);
            }

            return Rectangle.Empty;
        }

        /// <summary>
        /// Check for non-transparent pixels in a row, or portion of a row.
        /// Expands the size of the image rectangle if any were found.
        /// </summary>
        /// <returns>True if non-transparent pixels were found, false otherwise.</returns>
        unsafe private static bool ExpandImageRectangle(Surface surface, int y,
          int xStart, int xEnd, ref RectanglePosition rectPos)
        {
            bool fPixelFound = false;

            ColorBgra* rowStart = surface.GetRowAddress(y);
            ColorBgra* pixel = rowStart + xStart;
            for (int x = xStart; x < xEnd; x++)
            {
                if (pixel->A > 0)
                {
                    // Expand the rectangle to include the specified point.
                    if (x < rectPos.Left)
                        rectPos.Left = x;
                    if (x > rectPos.Right)
                        rectPos.Right = x;
                    if (y < rectPos.Top)
                        rectPos.Top = y;
                    if (y > rectPos.Bottom)
                        rectPos.Bottom = y;
                    fPixelFound = true;
                }
                pixel++;
            }

            return fPixelFound;
        }

        /// <summary>
        /// Checks that the block type matches the expected <see cref="PSPBlockID"/>.
        /// </summary>
        /// <param name="blockID">The block identifier.</param>
        /// <param name="expected">The expected <see cref="PSPBlockID"/>.</param>
        internal static void CheckBlockType(ushort blockID, PSPBlockID expected)
        {
            if (blockID != (ushort)expected)
            {
                throw new FormatException(string.Format(Properties.Resources.UnexpectedBlockTypeFormat,
                    blockID.ToString(CultureInfo.InvariantCulture),
                    ((ushort)expected).ToString(CultureInfo.InvariantCulture),
                    expected.ToString()));
            }
        }
    }
}
