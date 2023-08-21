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

namespace PaintShopProFiletype.PSPSections
{
    internal static class PSPConstants
    {
        /// <summary>
        /// Paint Shop Pro 5
        /// </summary>
        public const ushort majorVersion5 = 3;
        /// <summary>
        /// Paint Shop Pro 6
        /// </summary>
        public const ushort majorVersion6 = 4;
        /// <summary>
        /// Paint Shop Pro 7
        /// </summary>
        public const ushort majorVersion7 = 5;
        /// <summary>
        /// Paint Shop Pro 8
        /// </summary>
        public const ushort majorVersion8 = 6;
        /// <summary>
        /// Paint Shop Pro 9
        /// </summary>
        public const ushort majorVersion9 = 7;
        /// <summary>
        /// Paint Shop Pro 10
        /// </summary>
        public const ushort majorVersion10 = 8;
        /// <summary>
        /// Paint Shop Pro X2
        /// </summary>
        public const ushort majorVersion12 = 10;

        public const ushort v5ThumbnailBlock = 9;

        public const uint fieldIdentifier = 0x004c467e;
        public const uint blockIdentifier = 0x004b427e;
    }
}
