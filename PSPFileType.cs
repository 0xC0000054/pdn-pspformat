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

using System.Collections.Generic;
using System.IO;
using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using PaintShopProFiletype.Properties;

namespace PaintShopProFiletype
{
	[PaintDotNet.PluginSupportInfo(typeof(PluginSupportInfo))]
	public sealed class PaintShopProFormat : PropertyBasedFileType, IFileTypeFactory
	{
		public PaintShopProFormat() : base("Paint Shop Pro", FileTypeFlags.SupportsLayers | FileTypeFlags.SupportsLoading | FileTypeFlags.SupportsSaving | FileTypeFlags.SavesWithProgress, new string[] {".psp", ".pspimage", ".pspbrush", ".jfr", ".pspframe", ".pspmask",  ".tub", ".psptube" })
		{
		}

		public FileType[] GetFileTypeInstances()
		{
			return new FileType[] { new PaintShopProFormat() };
		}

		protected override Document OnLoad(Stream input)
		{
			PSPFile file = new PSPFile();
			return file.Load(input);
		}

		protected override bool IsReflexive(PropertyBasedSaveConfigToken token)
		{
			return true;
		}

		public override PropertyCollection OnCreateSavePropertyCollection()
		{
			List<Property> properties = new List<Property>()
			{
				StaticListChoiceProperty.CreateForEnum<FileVersion>(PropertyNames.FileVersion, FileVersion.Version6, false),
				StaticListChoiceProperty.CreateForEnum<CompressionFormats>(PropertyNames.CompressionType, CompressionFormats.LZ77, false)
			};

			return new PropertyCollection(properties);
		}

		public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
		{
			ControlInfo info = PropertyBasedFileType.CreateDefaultSaveConfigUI(props);

			info.SetPropertyControlValue(PropertyNames.FileVersion, ControlInfoPropertyNames.DisplayName, Resources.FileVersionText);
			info.FindControlForPropertyName(PropertyNames.FileVersion).SetValueDisplayName(FileVersion.Version5, Resources.Version5);
			info.FindControlForPropertyName(PropertyNames.FileVersion).SetValueDisplayName(FileVersion.Version6, Resources.Version6AndLater);

			info.FindControlForPropertyName(PropertyNames.CompressionType).SetValueDisplayName(CompressionFormats.None, Resources.CompressionFormatNoneText);
			info.FindControlForPropertyName(PropertyNames.CompressionType).SetValueDisplayName(CompressionFormats.LZ77, Resources.CompressionFormatLZ77Text);
			info.SetPropertyControlValue(PropertyNames.CompressionType, ControlInfoPropertyNames.DisplayName, Resources.CompressionFormatText);
			info.SetPropertyControlType(PropertyNames.CompressionType, PropertyControlType.RadioButton);

			return info;
		}


		private enum PropertyNames
		{
			FileVersion,
			CompressionType
		}

		protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
		{
			PSPFile file = new PSPFile();

			CompressionFormats format = (CompressionFormats)token.GetProperty(PropertyNames.CompressionType).Value;
			FileVersion version = (FileVersion)token.GetProperty(PropertyNames.FileVersion).Value;

			file.Save(input, output, format, scratchSurface, (ushort)version, progressCallback);
		}
	}
}
