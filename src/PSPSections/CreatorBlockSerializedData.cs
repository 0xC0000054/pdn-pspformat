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

using System.Runtime.Serialization;

#nullable enable

namespace PaintShopProFiletype.PSPSections
{
    [DataContract]
    internal sealed class CreatorBlockSerializedData
    {
        [DataMember(Order = 0)]
        private string title;
        [DataMember(Order = 1)]
        private uint createDate;
        [DataMember(Order = 2)]
        private string artist;
        [DataMember(Order = 3)]
        private string copyRight;
        [DataMember(Order = 4)]
        private string description;

        public CreatorBlockSerializedData()
        {
            this.title = string.Empty;
            this.createDate = 0;
            this.artist = string.Empty;
            this.copyRight = string.Empty;
            this.description = string.Empty;
        }

        public CreatorBlockSerializedData(CreatorBlock creatorBlock)
        {
            this.title = creatorBlock.Title;
            this.createDate = creatorBlock.CreateDate;
            this.artist = creatorBlock.Artist;
            this.copyRight = creatorBlock.CopyRight;
            this.description = creatorBlock.Description;
        }

        public string Title => this.title;

        public uint CreateDate => this.createDate;

        public string Artist => this.artist;

        public string CopyRight => this.copyRight;

        public string Description => this.description;
    }
}
