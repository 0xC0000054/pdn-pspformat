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

namespace PaintShopProFiletype.PSPSections
{
	internal static class RLE
	{
		public static byte[] Decompress(byte[] compressedData, uint uncompressedLength)
		{
			byte[] uncompressedData = new byte[uncompressedLength];

			int srcIndx = 0;
			int dstIndx = 0;

			int len = 0;
			byte value = 0;
			int compLength = compressedData.Length;

			while (srcIndx < compLength && dstIndx < uncompressedLength)
			{
				len = compressedData[srcIndx++];

				if (len > 128)
				{
					len -= 128;
					value = compressedData[srcIndx++];

					for (int i = dstIndx; i < dstIndx + len && i < uncompressedLength; i++)
					{
						uncompressedData[i] = value;
					}
				}
				else
				{
					for (int i = dstIndx; i < dstIndx + len && i < uncompressedLength && srcIndx < compLength; i++)
					{
						uncompressedData[i] = compressedData[srcIndx++];
					}
				}

				dstIndx += len;
			}

			return uncompressedData;
		}
	}
}
