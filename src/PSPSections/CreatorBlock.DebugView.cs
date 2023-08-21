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

using System;

#nullable enable

namespace PaintShopProFiletype.PSPSections
{
    internal sealed partial class CreatorBlock
    {
        private sealed class DebugView
        {
            private readonly CreatorBlock creatorBlock;

            public DebugView(CreatorBlock creatorBlock) => this.creatorBlock = creatorBlock;

            public string Title => this.creatorBlock.title;

            public DateTime CreateDate => UnixEpochLocal.AddSeconds(this.creatorBlock.createDate);

            public DateTime ModDate => UnixEpochLocal.AddSeconds(this.creatorBlock.modDate);

            public string Artist => this.creatorBlock.artist;

            public string CopyRight => this.creatorBlock.copyRight;

            public string Description => this.creatorBlock.description;
        }
    }
}
