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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PaintDotNet;
using System.Drawing;
using System.IO;

namespace PaintShopProFiletype.PSPSections
{
    static class PSPUtil
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
                this.lengthPos = writer.BaseStream.Position;
                this.writer.Write(0xBADF00DU);
                this.startPos = writer.BaseStream.Position;
                this.disposed = false;
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    long end = writer.BaseStream.Position;
                    long sectionLength = end - startPos;
                    
                    writer.BaseStream.Position = lengthPos;
                    writer.Write((uint)sectionLength);

                    writer.BaseStream.Position = end;

                    disposed = true;
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

    }
}
