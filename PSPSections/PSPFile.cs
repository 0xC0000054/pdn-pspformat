﻿// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See src/Resources/Files/License.txt for full licensing and attribution      //
// details.                                                                    //
// .                                                                           //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Ionic.Zlib;
using PaintDotNet;
using PaintDotNet.IO;
using PaintShopProFiletype.PSPSections;

namespace PaintShopProFiletype
{
	class PSPFile
	{
		private static byte[] PSPFileSig = new byte[32] {0x50, 0x61, 0x69, 0x6E, 0x74, 0x20, 0x53, 0x68, 0x6F, 0x70, 0x20, 0x50, 
		0x72, 0x6F, 0x20, 0x49, 0x6D, 0x61, 0x67, 0x65, 0x20, 0x46, 0x69, 0x6C, 0x65, 0x0A, 
		0x1A, 0x00, 0x00, 0x00, 0x00, 0x00}; // "Paint Shop Pro Image File\n\x1a” padded to 32 bytes

		private FileHeader fileHeader;
		private GeneralImageAttributes imageAttributes;
		private ExtendedDataBlock extData;
#if DEBUG
		private CreatorBlock creator;
#endif		
		private CompositeImageBlock compImage;        
		private ColorPaletteBlock globalPalette;
		private LayerBlock layerBlock;
		private ThumbnailBlock v5Thumbnail;

		private static bool CheckSig(byte[] sig)
		{
			for (int i = 0; i < 27; i++)
			{
				if (sig[i] != PSPFileSig[i])
				{
					return false;
				}
			}

			return true;
		}

		private static UserBlendOp BlendModetoBlendOp(PSPBlendModes mode)
		{
			switch (mode)
			{
				case PSPBlendModes.LAYER_BLEND_NORMAL:
					return new UserBlendOps.NormalBlendOp();
				case PSPBlendModes.LAYER_BLEND_DARKEN:
					return new UserBlendOps.DarkenBlendOp(); 
				case PSPBlendModes.LAYER_BLEND_LIGHTEN:
					return new UserBlendOps.LightenBlendOp(); 
				case PSPBlendModes.LAYER_BLEND_MULTIPLY:
					return new UserBlendOps.MultiplyBlendOp(); 
				case PSPBlendModes.LAYER_BLEND_SCREEN:
					return new UserBlendOps.ScreenBlendOp(); 
				case PSPBlendModes.LAYER_BLEND_OVERLAY:
					return new UserBlendOps.OverlayBlendOp(); 
				case PSPBlendModes.LAYER_BLEND_DIFFERENCE:
					return new UserBlendOps.DifferenceBlendOp(); 
				case PSPBlendModes.LAYER_BLEND_DODGE:
					return new UserBlendOps.ColorDodgeBlendOp(); 
				case PSPBlendModes.LAYER_BLEND_BURN:
					return new UserBlendOps.ColorBurnBlendOp(); 
				default:
					return new UserBlendOps.NormalBlendOp();
			}
		}

		private void LoadPSPFile(Stream input)
		{
			byte[] sigBytes = new byte[32];

			input.ProperRead(sigBytes, 0, sigBytes.Length);

			if (!CheckSig(sigBytes))
			{
				throw new FormatException(Properties.Resources.InvalidPSPFile);
			}

			using (BinaryReader br = new BinaryReader(input))
			{
				fileHeader = new FileHeader(br);

				while (input.Position < input.Length)
				{
					uint blockSig = br.ReadUInt32();
					PSPBlockID blockID = (PSPBlockID)br.ReadUInt16();
					uint initialBlockLength = fileHeader.Major <= PSPConstants.majorVersion5 ? br.ReadUInt32() : 0;
					uint blockLength = br.ReadUInt32();

					switch (blockID)
					{
						case PSPBlockID.PSP_IMAGE_BLOCK:
							imageAttributes = new GeneralImageAttributes(br, fileHeader.Major);
							break;
#if DEBUG
						case PSPBlockID.PSP_CREATOR_BLOCK:
							creator = new CreatorBlock(br, blockLength);
							break;
#endif
						case PSPBlockID.PSP_COLOR_BLOCK:
							globalPalette = new ColorPaletteBlock(br, fileHeader.Major);
							break;
						case PSPBlockID.PSP_LAYER_START_BLOCK:
							this.layerBlock = new LayerBlock(br, imageAttributes, fileHeader.Major);
							break;
						case PSPBlockID.PSP_EXTENDED_DATA_BLOCK:
							extData = new ExtendedDataBlock(br, blockLength);
							break;
#if DEBUG
						case PSPBlockID.PSP_COMPOSITE_IMAGE_BANK_BLOCK:
							this.compImage = new CompositeImageBlock(br, fileHeader.Major);
							break;
#endif
						default:

							br.BaseStream.Position += (long)blockLength;
							break;
					}

				}
			}
		}

		private static unsafe byte[] ExpandPackedPalette(Rectangle saveRect, LayerBitmapInfoChunk bitmap, int bitDepth)
		{
			int height = saveRect.Height;
			int width = saveRect.Width;
			byte[] image = new byte[width * height];
 
			int bpp, shift;
			if (bitDepth == 4)
			{
				bpp = 2;
				shift = 1;
			}
			else
			{
				bpp = 8;
				shift = 3;
			}

			fixed (byte* ptr = image, dPtr = bitmap.channels[0].channelData)
			{
				int srcStride = width / bpp;

				for (int y = 0; y < height; y++)
				{
					byte* src = dPtr + (y * srcStride);
					byte* dst = ptr + (y * width);

					for (int x = 0; x < width; x++)
					{
						byte data = src[x >> shift];

						switch (bitDepth)
						{
							case 4:

								if ((x & 1) == 1) // odd column
								{
									*dst = (byte)(data & 0x0f); 
								}
								else
								{
									*dst = (byte)(data >> 4); 
								}
							   
								break;
							case 1:

								int mask = x & 7;
								// extract the palette bit for the current pixel. 
								*dst = (byte)((data & (128 >> mask)) >> (7 - mask)); 
								break;

						}

						dst++;
					}
				}



			}


			return image;
		}

		public Document Load(Stream input)
		{
			this.LoadPSPFile(input);

#if DEBUG
			Debug.Assert(imageAttributes.Width > 0 && imageAttributes.Height > 0);
#endif
			Document doc = new Document(imageAttributes.Width, imageAttributes.Height);

			if (imageAttributes.ResValue > 0.0)
			{
				switch (imageAttributes.ResUnit)
				{
					case PSP_METRIC.PSP_METRIC_INCH:                        
						doc.DpuUnit = MeasurementUnit.Inch;
						doc.DpuX = doc.DpuY = imageAttributes.ResValue;
						break;
					case PSP_METRIC.PSP_METRIC_CM:
						doc.DpuUnit = MeasurementUnit.Centimeter;
						doc.DpuX = doc.DpuY = imageAttributes.ResValue;
						break;
					default:
						break;
				}
			}
			
			int layerCount = layerBlock.LayerInfo.Length;
			LayerInfoChunk[] infoChunks = layerBlock.LayerInfo;
			LayerBitmapInfoChunk[] bitmapInfoChunks =  layerBlock.LayerBitmapInfo;

			for (int i = 0; i < layerCount; i++)
			{
				LayerInfoChunk info = infoChunks[i];
				LayerBitmapInfoChunk bitmapInfo = bitmapInfoChunks[i];

#if DEBUG
				Debug.Assert(info.imageRect.Width > 0 && info.imageRect.Height > 0);
#endif

				if (info.type == PSPLayerType.keGLTRaster || info.type == PSPLayerType.keGLTFloatingRasterSelection)
				{
					BitmapLayer layer = new BitmapLayer(info.imageRect.Width, info.imageRect.Height) { Name = info.name, Opacity = info.opacity };
					UserBlendOp blendOp = BlendModetoBlendOp(info.blendMode);
					layer.SetBlendOp(blendOp);
					layer.Visible = (info.layerFlags & PSPLayerProperties.keVisibleFlag) == PSPLayerProperties.keVisibleFlag;

					Rectangle saveRect = info.saveRect;

					short transIndex = -1;

					if (imageAttributes.BitDepth < 24 && extData.Values.ContainsKey(PSPExtendedDataID.PSP_XDATA_TRNS_INDEX))
					{
						transIndex = BitConverter.ToInt16(extData.Values[PSPExtendedDataID.PSP_XDATA_TRNS_INDEX], 0);
					}

					if (bitmapInfo.bitmapCount == 1)
					{
						new UnaryPixelOps.SetAlphaChannelTo255().Apply(layer.Surface, saveRect);
					}
					
					int alphaIndex = 0;

					int bitDepth = imageAttributes.BitDepth;                    

					int bpp = 1;
					int stride = saveRect.Width;
					if (bitDepth == 48)
					{
						bpp = 2;
						stride *= 2;
					}

					byte[] expandedPalette = null;
					if (bitDepth == 4 || bitDepth == 1)
					{
						expandedPalette = ExpandPackedPalette(saveRect, bitmapInfo, bitDepth);
					}

					unsafe
					{
						int palIdx = 0;
						NativeStructs.RGBQUAD entry;

						Surface surface = layer.Surface;
						for (int y = saveRect.Top; y < saveRect.Bottom; y++)
						{
							ColorBgra* ptr = surface.GetPointAddressUnchecked(saveRect.Left, y);
							ColorBgra* endPtr = ptr + saveRect.Width;
							int index = ((y - saveRect.Top) * stride);

							while (ptr < endPtr)
							{
								switch (bitDepth)
								{
									case 48:

										for (int ci = 0; ci < bitmapInfo.channelCount; ci++)
										{
											ChannelSubBlock ch = bitmapInfo.channels[ci];

											ushort col = (ushort)(ch.channelData[index] | (ch.channelData[index + 1] << 8)); // PSP format is always little endian 
											byte clamped = (byte)((col * 255) / 65535);

											if (ch.bitmapType == PSPDIBType.PSP_DIB_IMAGE)
											{
												switch (ch.channelType)
												{
													case PSPChannelType.PSP_CHANNEL_RED:
														ptr->R = clamped;
														break;
													case PSPChannelType.PSP_CHANNEL_GREEN:
														ptr->G = clamped;
														break;
													case PSPChannelType.PSP_CHANNEL_BLUE:
														ptr->B = clamped;
														break;
												} 
											}
											else if (ch.bitmapType == PSPDIBType.PSP_DIB_TRANS_MASK)
											{
												ptr->A = ch.channelData[alphaIndex];										
                                                alphaIndex++;
											}


										}

										break;
									case 24:

										for (int ci = 0; ci < bitmapInfo.channelCount; ci++)
										{
											ChannelSubBlock ch = bitmapInfo.channels[ci];

											if (ch.bitmapType == PSPDIBType.PSP_DIB_IMAGE)
											{
												switch (ch.channelType)
												{
													case PSPChannelType.PSP_CHANNEL_RED:
														ptr->R = ch.channelData[index];
														break;
													case PSPChannelType.PSP_CHANNEL_GREEN:
														ptr->G = ch.channelData[index];
														break;
													case PSPChannelType.PSP_CHANNEL_BLUE:
														ptr->B = ch.channelData[index];
														break;
												}
											}
											else if (ch.bitmapType == PSPDIBType.PSP_DIB_TRANS_MASK)
											{
												ptr->A = ch.channelData[index];
											}

										}

										break;
									case 8:
										for (int ci = 0; ci < bitmapInfo.channelCount; ci++)
										{
											ChannelSubBlock ch = bitmapInfo.channels[ci];
											palIdx = ch.channelData[index];
											entry = this.globalPalette.entries[palIdx];

											switch (ch.bitmapType)
											{
												case PSPDIBType.PSP_DIB_IMAGE:
													ptr->R = entry.rgbRed;
													ptr->G = entry.rgbGreen;
													ptr->B = entry.rgbBlue;

													if (palIdx == transIndex)
													{
														ptr->A = 0;
													}

													break;
												case PSPDIBType.PSP_DIB_TRANS_MASK:
													ptr->A = entry.rgbRed;
													break;
											}
										}

										break;
									case 4:
									case 1:

										palIdx = expandedPalette[index];
										entry = this.globalPalette.entries[palIdx];
										
										ptr->R = entry.rgbRed;
										ptr->G = entry.rgbGreen;
										ptr->B = entry.rgbBlue;

										if (palIdx == transIndex)
										{
											ptr->A = 0;
										}
										else if ((bitmapInfo.bitmapCount == 2) && bitmapInfo.channels[1].bitmapType == PSPDIBType.PSP_DIB_TRANS_MASK)
										{
											ptr->A = bitmapInfo.channels[1].channelData[index];                                            
										}

										break;
									default:
										throw new FormatException(string.Format(Properties.Resources.UnsupportedBitDepth, bitDepth));

								}

								ptr++;
								index += bpp;
							}
						} 
					}

#if DEBUG
					using (Bitmap temp = layer.Surface.CreateAliasedBitmap())
					{

					} 
#endif
					
					doc.Layers.Add(layer);
				}
			   
			}
		

			return doc;
		}

		private static PSPBlendModes BlendOptoBlendMode(UserBlendOp op)
		{
			Type opType = op.GetType();

			if (opType == typeof(UserBlendOps.NormalBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_NORMAL;
			}
			else if (opType == typeof(UserBlendOps.ColorBurnBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_BURN;
			}
			else if (opType == typeof(UserBlendOps.ColorDodgeBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_DODGE;
			}
			else if (opType == typeof(UserBlendOps.DarkenBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_DARKEN;
			}
			else if (opType == typeof(UserBlendOps.DifferenceBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_DIFFERENCE;
			}
			else if (opType == typeof(UserBlendOps.LightenBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_LIGHTEN;
			}
			else if (opType == typeof(UserBlendOps.MultiplyBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_MULTIPLY;
			}
			else if (opType == typeof(UserBlendOps.OverlayBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_OVERLAY;
			}
			else if (opType == typeof(UserBlendOps.ScreenBlendOp))
			{
				return PSPBlendModes.LAYER_BLEND_SCREEN;
			} 

			return PSPBlendModes.LAYER_BLEND_NORMAL;
		}

		private static PSPCompression CompressionFromTokenFormat(CompressionFormats format)
		{
			PSPCompression comp = PSPCompression.PSP_COMP_NONE;

			switch (format)
			{
				case CompressionFormats.LZ77:
					comp = PSPCompression.PSP_COMP_LZ77;
					break;
			}

			return comp;
		}

		private int doneProgress;
		private int totalProgress;
		private unsafe ChannelSubBlock[] SplitImageChannels(Surface source, Rectangle savedBounds, int channelCount, ushort majorVersion, bool composite, ProgressEventHandler callback)
		{
			ChannelSubBlock[] channels = new ChannelSubBlock[channelCount];
			
			int channelSize = (savedBounds.Width * savedBounds.Height);

			for (int i = 0; i < channelCount; i++)
			{
				channels[i].chunkSize = majorVersion > PSPConstants.majorVersion5 ? 16U : 12U;
				if (composite)
				{
					switch (majorVersion)
					{
						case PSPConstants.majorVersion5:
							channels[i].bitmapType = PSPDIBType.PSP_DIB_THUMBNAIL;
							break;
						default:					
							channels[i].bitmapType = i < 3 ? PSPDIBType.PSP_DIB_COMPOSITE : PSPDIBType.PSP_DIB_COMPOSITE_TRANS_MASK;
							break;
					}

				}
				else
				{
					channels[i].bitmapType = i < 3 ? PSPDIBType.PSP_DIB_IMAGE : PSPDIBType.PSP_DIB_TRANS_MASK;
				}
				
				channels[i].channelData = null;
				channels[i].compressedChannelLength = 0;
				channels[i].uncompressedChannelLength = (uint)channelSize;
				switch (i)
				{
					case 0:
						channels[i].channelType = PSPChannelType.PSP_CHANNEL_RED;
						break;
					case 1:
						channels[i].channelType = PSPChannelType.PSP_CHANNEL_GREEN;
						break;
					case 2: 
						channels[i].channelType = PSPChannelType.PSP_CHANNEL_BLUE;
						break;
					case 3:
						channels[i].channelType = PSPChannelType.PSP_CHANNEL_COMPOSITE;
						break;
				}
			}

			if (channelSize > 0)
			{
				byte[] red = new byte[channelSize];
				byte[] green = new byte[channelSize];
				byte[] blue = new byte[channelSize];
				byte[] alpha = channelCount == 4 ? new byte[channelSize] : null;

				for (int y = savedBounds.Top; y < savedBounds.Bottom; y++)
				{
					ColorBgra* p = source.GetPointAddressUnchecked(savedBounds.Left, y);
					int index = ((y - savedBounds.Top) * savedBounds.Width);
					for (int x = savedBounds.Left; x < savedBounds.Right; x++)
					{
						red[index] = p->R;
						green[index] = p->G;
						blue[index] = p->B;
						if (channelCount == 4)
						{
							alpha[index] = p->A;
						}
						p++;
						index++;
					}
				}

				for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
				{
					byte[] inData = null;

					switch (channelIndex)
					{
						case 0:
							inData = red;
							break;
						case 1:
							inData = green;
							break;
						case 2:
							inData = blue;
							break;
						case 3:
							inData = alpha;
							break;
					}


					byte[] compBuffer = null;

					switch (imageAttributes.CompressionType)
					{
						case PSPCompression.PSP_COMP_NONE:
							compBuffer = inData;
							channels[channelIndex].uncompressedChannelLength = 0;
							break;
						case PSPCompression.PSP_COMP_LZ77:

							using (MemoryStream ms = new MemoryStream())
							{
								using (ZlibStream zs = new ZlibStream(ms, CompressionMode.Compress, CompressionLevel.Level9, true))
								{
									int length = inData.Length;
									int offset = 0;

									do
									{
										int chunkSize = Math.Min(ZlibConstants.WorkingBufferSizeDefault, length);

										zs.Write(inData, offset, chunkSize);

										offset += chunkSize;
										length -= chunkSize;

									} while (length > 0);

								}
								compBuffer = ms.ToArray();
							}
							break;
					}

					if (callback != null)
					{
						doneProgress++;

						callback(this, new ProgressEventArgs(100.0 * ((double)doneProgress / (double)totalProgress)));
					}

					channels[channelIndex].compressedChannelLength = (uint)compBuffer.Length;
					channels[channelIndex].channelData = compBuffer;

				} 
			}

			return channels;
		}

		public static Size GetThumbnailDimensions(int originalWidth, int originalHeight, int maxEdgeLength)
		{
			Size thumbSize = Size.Empty;
			
			if (originalWidth <= 0 || originalHeight <= 0)
			{
				thumbSize.Width = 1;
				thumbSize.Height = 1;
			}
			else if (originalWidth > originalHeight)
			{
				int longSide = Math.Min(originalWidth, maxEdgeLength);
				thumbSize.Width = longSide;
				thumbSize.Height = Math.Max(1, (originalHeight * longSide) / originalWidth);
			}
			else if (originalHeight > originalWidth)
			{
				int longSide = Math.Min(originalHeight, maxEdgeLength);
				thumbSize.Width = Math.Max(1, (originalWidth * longSide) / originalHeight);
				thumbSize.Height= longSide;
			}
			else 
			{
				int longSide = Math.Min(originalWidth, maxEdgeLength);
				thumbSize.Width = longSide;
				thumbSize.Height = longSide;
			}

			return thumbSize;
		}

		private static unsafe bool LayerHasTransparency(Surface surface, Rectangle bounds)
		{
			for (int y = bounds.Top; y < bounds.Bottom; y++)
			{
				ColorBgra* startPtr = surface.GetPointAddressUnchecked(bounds.Left, y);
				ColorBgra* endPtr = startPtr + bounds.Width;

				while (startPtr < endPtr)
				{
					if (startPtr->A < 255)
					{
						return true;
					}

					startPtr++;
				}
			}

			return false;
		}

		public void Save(Document input, Stream output, CompressionFormats format, Surface scratchSurface, ushort majorVersion, ProgressEventHandler callback)
		{

			if (majorVersion == PSPConstants.majorVersion5 && input.Layers.Count > 64)
			{
				throw new FormatException(string.Format(Properties.Resources.MaxLayersFormat, 64));
			}
			else if ((majorVersion == PSPConstants.majorVersion6 || majorVersion == PSPConstants.majorVersion7) && input.Layers.Count > 100)
			{
				throw new FormatException(string.Format(Properties.Resources.MaxLayersFormat, 100));
			}

			using (BinaryWriterEx writer = new BinaryWriterEx(output, false))
			{
				this.fileHeader = new FileHeader(majorVersion);
				this.imageAttributes = new GeneralImageAttributes(input.Width, input.Height, CompressionFromTokenFormat(format), 0, input.Layers.Count, majorVersion);
				switch (input.DpuUnit)
				{
					case MeasurementUnit.Centimeter:
						this.imageAttributes.ResValue = input.DpuX;
						this.imageAttributes.ResUnit = PSP_METRIC.PSP_METRIC_CM;
						break;
					case MeasurementUnit.Inch:
						this.imageAttributes.ResValue = input.DpuX;
						this.imageAttributes.ResUnit = PSP_METRIC.PSP_METRIC_INCH;
						break;
				}

				this.totalProgress = 0;
				this.doneProgress = 0;

				bool flatImage = true;
				foreach (Layer item in input.Layers)
				{
					BitmapLayer layer = (BitmapLayer)item;

					Rectangle rect = PSPUtil.GetImageSaveRectangle(layer.Surface);

					if (!rect.IsEmpty)
					{
						if (LayerHasTransparency(layer.Surface, rect))
						{
							totalProgress += 4;
							flatImage = false;
						}
						else
						{
							totalProgress += 3;
						} 
					}

				}
				
				if (flatImage)
				{
					this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.keGCFlatImage);
				}

				if (majorVersion > PSPConstants.majorVersion5)
				{

					CompositeImageAttributesChunk normAttr = new CompositeImageAttributesChunk()
					{
						chunkSize = 24,
						width = input.Width,
						height = input.Height,
						bitDepth = 24,
						colorCount = (1 << 24),
						compositeImageType = PSPCompositeImageType.PSP_IMAGE_COMPOSITE,
						compressionType = imageAttributes.CompressionType,
						planeCount = 1,
					};
					CompositeImageAttributesChunk jpgAttr = new CompositeImageAttributesChunk()
					{
						chunkSize = 24,
						bitDepth = 24,
						colorCount = (1 << 24),
						compositeImageType = PSPCompositeImageType.PSP_IMAGE_THUMBNAIL,
						compressionType = PSPCompression.PSP_COMP_JPEG,
						planeCount = 1
					};
					JPEGCompositeInfoChunk jpgChunk = new JPEGCompositeInfoChunk()
					{
						chunkSize = 14,
						unCompressedSize = 0,
						imageType = PSPDIBType.PSP_DIB_THUMBNAIL
					};
					CompositeImageInfoChunk infoChunk = new CompositeImageInfoChunk()
					{
						chunkSize = 8,
						bitmapCount = 1,
						channelCount = 3,
						paletteSubBlock = null
					};

					using (RenderArgs args = new RenderArgs(scratchSurface))
					{
						input.Render(args, true);


						int channelCount = 3;
						if (LayerHasTransparency(args.Surface, args.Surface.Bounds))
						{
							channelCount = 4;
							infoChunk.channelCount = 4;
							infoChunk.bitmapCount = 2;
						}
#if DEBUG
						using (Bitmap bmp = args.Surface.CreateAliasedBitmap())
						{

						} 
#endif

						this.totalProgress += channelCount;

						infoChunk.channelBlocks = SplitImageChannels(args.Surface, args.Surface.Bounds, channelCount, majorVersion, true, callback);

						Size scaleSize = GetThumbnailDimensions(input.Width, input.Height, 200);

						jpgAttr.width = scaleSize.Width;
						jpgAttr.height = scaleSize.Height;

						using (Surface fit = new Surface(scaleSize))
						{
							fit.FitSurface(ResamplingAlgorithm.SuperSampling, args.Surface);

							if (channelCount == 4)
							{
								unsafe
								{
									for (int y = 0; y < jpgAttr.height; y++)
									{
										ColorBgra* ptr = fit.GetRowAddressUnchecked(y);
										ColorBgra* endPtr = ptr + jpgAttr.width;

										while (ptr < endPtr)
										{
											if (ptr->A == 0)
											{
												ptr->Bgra |= 0x00ffffff; // set the color of the transparent pixels to white, same as Paint Shop Pro  
											}

											ptr++;
										}
									}

								}
							}


							using (Bitmap temp = fit.CreateAliasedBitmap(false))
							{
								using (MemoryStream stream = new MemoryStream())
								{
									temp.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);

									jpgChunk.imageData = stream.ToArray();
									jpgChunk.compressedSize = (uint)stream.Length;
								}
							}
						}

					}

					this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.keGCComposite);
					this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.keGCThumbnail);

					if (infoChunk.bitmapCount == 2)
					{
						this.imageAttributes.SetGraphicContentFlag(PSPGraphicContents.keGCCompositeTransparency);
					}

					this.compImage = new CompositeImageBlock(new CompositeImageAttributesChunk[2] { jpgAttr, normAttr }, jpgChunk, infoChunk);
				}
				else
				{
					Size scaleSize = GetThumbnailDimensions(input.Width, input.Height, 300);

					this.v5Thumbnail = new ThumbnailBlock()
					{ 
						width  = scaleSize.Width,
						height = scaleSize.Height,
						bitDepth = 24,
						compressionType = PSPCompression.PSP_COMP_LZ77,
						planeCount = 1,
						colorCount = (1 << 24),
						paletteEntryCount = 0,
						channelCount = 3
					};

					scratchSurface.Clear(ColorBgra.White);
					using (RenderArgs args = new RenderArgs(scratchSurface))
					{
						input.Render(args, false);
						using (Surface fit = new Surface(scaleSize))
						{
							fit.FitSurface(ResamplingAlgorithm.SuperSampling, args.Surface);

							this.totalProgress += 3;

							this.v5Thumbnail.channelBlocks = SplitImageChannels(args.Surface, args.Surface.Bounds, 3, majorVersion, true, callback);
						}
					}

				}

				int layerCount = input.Layers.Count;                
		   
				LayerInfoChunk[] layerInfoChunks = new LayerInfoChunk[layerCount];
				LayerBitmapInfoChunk[] layerBitmapChunks = new LayerBitmapInfoChunk[layerCount];

				for (int i = 0; i < layerCount; i++)
				{
					BitmapLayer layer = (BitmapLayer)input.Layers[i];

					Rectangle savedBounds = PSPUtil.GetImageSaveRectangle(layer.Surface);

					LayerInfoChunk infoChunk = new LayerInfoChunk()
					{
						fileMajorVersion = majorVersion, // used to determine the version to save as
						imageRect = layer.Bounds,
						name = layer.Name,
						opacity = layer.Opacity,
						saveRect = savedBounds,
						type = majorVersion > PSPConstants.majorVersion5 ? PSPLayerType.keGLTRaster : 0
						// the LAYER_TYPE_NORMAL value in PSP 5 and older is 0 
					};
					infoChunk.chunkSize = majorVersion > PSPConstants.majorVersion5 ? (uint)(119 + 2 + layer.Name.Length) : 375;
					infoChunk.blendMode = BlendOptoBlendMode(layer.BlendOp);
					if (layer.Visible)
					{
						infoChunk.layerFlags |= PSPLayerProperties.keVisibleFlag;
					}
					layerInfoChunks[i] = infoChunk;

					if (!savedBounds.IsEmpty)
					{
						int channelCount = LayerHasTransparency(layer.Surface, savedBounds) ? 4 : 3;
						int bitmapCount = channelCount == 4 ? 2 : 1;
						LayerBitmapInfoChunk biChunk = new LayerBitmapInfoChunk()
						{
							chunkSize = 8U,
							bitmapCount = (ushort)bitmapCount,
							channelCount = (ushort)channelCount
						};
						biChunk.channels = SplitImageChannels(layer.Surface, savedBounds, channelCount, majorVersion, false, callback);

						if (majorVersion <= PSPConstants.majorVersion5)
						{
							infoChunk.v5BitmapCount = biChunk.bitmapCount;
							infoChunk.v5ChannelCount = biChunk.channelCount;
						} 		
						
						layerBitmapChunks[i] = biChunk;
					}

				}

				this.layerBlock = new LayerBlock(layerInfoChunks, layerBitmapChunks);

				writer.Write(PSPFileSig);
				this.fileHeader.Save(writer);
				this.imageAttributes.Save(writer);
				if (this.compImage != null)
				{
					this.compImage.Save(writer);
				}
				else if (this.v5Thumbnail != null)
				{
					this.v5Thumbnail.Save(writer);
				}
				this.layerBlock.Save(writer, majorVersion);
			}

		}
	}
}