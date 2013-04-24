using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PaintShopProFiletype.PSPSections
{
    class PSPConstants
    {
        private PSPConstants()
        {
        }

        public const ushort majorVersion5 = 3;
        public const ushort majorVersion6 = 4;
        public const ushort majorVersion7 = 5;
        public const ushort majorVersion8 = 6;
        public const ushort majorVersion9 = 7;
        public const ushort majorVersion10 = 8;

        public const ushort v5ThumbnailBlock = 9;

        public const uint fieldIdentifier = 0x004c467e;
        public const uint blockIdentifier = 0x004b427e;
    }
}
