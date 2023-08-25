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
using System.Collections.Generic;
using System.IO;
using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using PaintShopProFiletype.Properties;

namespace PaintShopProFiletype
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class PaintShopProFormat : PropertyBasedFileType
    {
        private static readonly string[] FileExtensions = new string[] { ".psp", ".pspimage", ".pspbrush", ".jfr", ".pspframe", ".pspmask", ".tub", ".psptube" };

        private readonly IServiceProvider services;

        public PaintShopProFormat(IServiceProvider services) : base(
            "Paint Shop Pro",
            new FileTypeOptions()
            {
                SupportsLayers = true,
                LoadExtensions = FileExtensions,
                SaveExtensions = FileExtensions,
            })
        {
            this.services = services;
        }

        protected override Document OnLoad(Stream input)
        {
            PSPFile file = new PSPFile(this.services);
            return file.Load(input);
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> properties = new List<Property>()
            {
                StaticListChoiceProperty.CreateForEnum(PropertyNames.FileVersion, FileVersion.Version6, false),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.CompressionType, CompressionFormats.LZ77, false)
            };

            return new PropertyCollection(properties);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo info = PropertyBasedFileType.CreateDefaultSaveConfigUI(props);

            PropertyControlInfo fileVersionPCI = info.FindControlForPropertyName(PropertyNames.FileVersion)!;

            fileVersionPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = Resources.FileVersionText;
            fileVersionPCI.SetValueDisplayName(FileVersion.Version5, Resources.Version5);
            fileVersionPCI.SetValueDisplayName(FileVersion.Version6, Resources.Version6AndLater);

            PropertyControlInfo compressonTypePCI = info.FindControlForPropertyName(PropertyNames.CompressionType)!;

            compressonTypePCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = Resources.CompressionFormatText;
            compressonTypePCI.ControlType.Value = PropertyControlType.RadioButton;
            compressonTypePCI.SetValueDisplayName(CompressionFormats.None, Resources.CompressionFormatNoneText);
            compressonTypePCI.SetValueDisplayName(CompressionFormats.LZ77, Resources.CompressionFormatLZ77Text);

            return info;
        }

        private enum PropertyNames
        {
            FileVersion,
            CompressionType
        }

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            PSPFile file = new PSPFile(this.services);

            CompressionFormats format = (CompressionFormats)token.GetProperty(PropertyNames.CompressionType)!.Value!;
            FileVersion version = (FileVersion)token.GetProperty(PropertyNames.FileVersion)!.Value!;

            file.Save(input, output, format, scratchSurface, (ushort)version, progressCallback);
        }
    }
}
