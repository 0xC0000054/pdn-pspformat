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

using PaintDotNet;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;

#nullable enable

namespace PaintShopProFiletype.PSPSections
{
    internal static class CreatorBlockSerialization
    {
        private const string PSPCreatorMetaDataBinaryFormatter = "PSPFormatCreatorData";
        private const string PSPCreatorMetaDataDataContract = "PSPFormatCreatorData2";

        public static CreatorBlock Deserialize(Document input)
        {
            CreatorBlock result;

            string? creatorData = input.Metadata.GetUserValue(PSPCreatorMetaDataDataContract);
            if (!string.IsNullOrEmpty(creatorData))
            {
                result = DeserializeDataContract(creatorData);
            }
            else
            {
                creatorData = input.Metadata.GetUserValue(PSPCreatorMetaDataBinaryFormatter);
                if (!string.IsNullOrEmpty(creatorData))
                {
                    result = DeserializeBinaryFormatter(creatorData);
                }
                else
                {
                    result = new CreatorBlock();
                }
            }

            return result;
        }

        public static void Serialize(CreatorBlock? input, Document document)
        {
            if (input is null)
            {
                return;
            }

            string data = SerializeDataContract(input);

            document.Metadata.SetUserValue(PSPCreatorMetaDataDataContract, data);
        }

        private static CreatorBlock DeserializeBinaryFormatter(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);

            using (MemoryStream stream = new MemoryStream(bytes))
            {
#pragma warning disable SYSLIB0011
                BinaryFormatter formatter = new BinaryFormatter() { Binder = new SelfBinder() };
                return (CreatorBlock)formatter.Deserialize(stream);
#pragma warning restore SYSLIB0011
            }
        }

        private static CreatorBlock DeserializeDataContract(string text)
        {
            CreatorBlock result;

            using (StringReader stringReader = new StringReader(text))
            using (XmlReader xmlReader = XmlReader.Create(stringReader))
            {
                result = (CreatorBlock)new DataContractSerializer(typeof(CreatorBlock)).ReadObject(xmlReader)!;
            }

            return result;
        }

        private static string SerializeDataContract(CreatorBlock creatorBlock)
        {
            StringBuilder builder = new StringBuilder();

            using (XmlWriter writer = XmlWriter.Create(builder))
            {
                new DataContractSerializer(typeof(CreatorBlock)).WriteObject(writer, creatorBlock);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Binds the serialization to types in the currently loaded assembly.
        /// </summary>
        private class SelfBinder : SerializationBinder
        {
            public override Type? BindToType(string assemblyName, string typeName)
            {
                return Type.GetType(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", typeName, assemblyName));
            }
        }
    }
}
