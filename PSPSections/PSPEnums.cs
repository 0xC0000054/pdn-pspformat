// PSP File Format Specification is Copyright (C) 2000 Jasc Software, Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PaintShopProFiletype.PSPSections
{
    /* Block identifiers.
    */
    enum PSPBlockID
    {
        PSP_IMAGE_BLOCK = 0, // General Image Attributes Block (main)
        PSP_CREATOR_BLOCK, // Creator Data Block (main)
        PSP_COLOR_BLOCK, // Color Palette Block (main and sub)
        PSP_LAYER_START_BLOCK, // Layer Bank Block (main)
        PSP_LAYER_BLOCK, // Layer Block (sub)
        PSP_CHANNEL_BLOCK, // Channel Block (sub)
        PSP_SELECTION_BLOCK, // Selection Block (main)
        PSP_ALPHA_BANK_BLOCK, // Alpha Bank Block (main)
        PSP_ALPHA_CHANNEL_BLOCK,// Alpha Channel Block (sub)
        PSP_COMPOSITE_IMAGE_BLOCK, // Composite Image Block (sub)
        PSP_EXTENDED_DATA_BLOCK,// Extended Data Block (main)
        PSP_TUBE_BLOCK, // Picture Tube Data Block (main)
        PSP_ADJUSTMENT_EXTENSION_BLOCK, // Adjustment Layer Block (sub)
        PSP_VECTOR_EXTENSION_BLOCK, // Vector Layer Block (sub)
        PSP_SHAPE_BLOCK, // Vector Shape Block (sub)
        PSP_PAINTSTYLE_BLOCK, // Paint Style Block (sub)
        PSP_COMPOSITE_IMAGE_BANK_BLOCK, // Composite Image Bank (main)
        PSP_COMPOSITE_ATTRIBUTES_BLOCK, // Composite Image Attr. (sub)
        PSP_JPEG_BLOCK, // JPEG Image Block (sub)
        PSP_LINESTYLE_BLOCK, // Line Style Block (sub)
        PSP_TABLE_BANK_BLOCK, // Table Bank Block (main)
        PSP_TABLE_BLOCK, // Table Block (sub)
        PSP_PAPER_BLOCK, // Vector Table Paper Block (sub)
        PSP_PATTERN_BLOCK, // Vector Table Pattern Block (sub)
        PSP_GROUP_EXTENSION_BLOCK, // Group Layer Block (sub)
        PSP_MASK_EXTENSION_BLOCK, // Mask Layer Block (sub)
        PSP_BRUSH_BLOCK, // Brush Data Block (main)
    }

    /* Bitmap types.
    */
    enum PSPDIBType
    {
        PSP_DIB_IMAGE = 0, // Layer color bitmap
        PSP_DIB_TRANS_MASK, // Layer transparency mask bitmap
        PSP_DIB_USER_MASK, // Layer user mask bitmap
        PSP_DIB_SELECTION, // Selection mask bitmap
        PSP_DIB_ALPHA_MASK, // Alpha channel mask bitmap
        PSP_DIB_THUMBNAIL, // Thumbnail bitmap
        PSP_DIB_THUMBNAIL_TRANS_MASK, // Thumbnail transparency mask
        PSP_DIB_ADJUSTMENT_LAYER, // Adjustment layer bitmap
        PSP_DIB_COMPOSITE, // Composite image bitmap
        PSP_DIB_COMPOSITE_TRANS_MASK, // Composite image transparency
        PSP_DIB_PAPER, // Paper bitmap
        PSP_DIB_PATTERN, // Pattern bitmap
        PSP_DIB_PATTERN_TRANS_MASK, // Pattern transparency mask
    };
    /* Type of image in the composite image bank block.
    */
    enum PSPCompositeImageType
    {
        PSP_IMAGE_COMPOSITE = 0, // Composite Image
        PSP_IMAGE_THUMBNAIL, // Thumbnail Image
    };
    /* Channel types.
    */
    enum PSPChannelType
    {
        PSP_CHANNEL_COMPOSITE = 0, // Channel of single channel bitmap
        PSP_CHANNEL_RED, // Red channel of 24-bit bitmap
        PSP_CHANNEL_GREEN, // Green channel of 24-bit bitmap
        PSP_CHANNEL_BLUE, // Blue channel of 24-bit bitmap
    };
    /* Possible types of compression.
    */
    enum PSPCompression
    {
        PSP_COMP_NONE = 0, // No compression
        PSP_COMP_RLE, // RLE compression
        PSP_COMP_LZ77, // LZ77 compression
        PSP_COMP_JPEG // JPEG compression (only used by thumbnail and composite image)
    };
    
    /* Layer types.
    */
    enum PSPLayerType
    {
        keGLTUndefined = 0, // Undefined layer type
        keGLTRaster, // Standard raster layer
        keGLTFloatingRasterSelection, // Floating selection (raster)
        keGLTVector, // Vector layer
        keGLTAdjustment // Adjustment layer
    }

    /* Layer flags.
    */
    [Flags]
    enum PSPLayerProperties
    {
        keVisibleFlag = 1, // Layer is visible
        keMaskPresenceFlag = 2 // Layer has a mask
    }

    /* Blend modes.
    */
    enum PSPBlendModes
    {
        LAYER_BLEND_NORMAL,
        LAYER_BLEND_DARKEN,
        LAYER_BLEND_LIGHTEN,
        LAYER_BLEND_LEGACY_HUE,
        LAYER_BLEND_LEGACY_SATURATION,
        LAYER_BLEND_LEGACY_COLOR,
        LAYER_BLEND_LEGACY_LUMINOSITY,
        LAYER_BLEND_MULTIPLY,
        LAYER_BLEND_SCREEN,
        LAYER_BLEND_DISSOLVE,
        LAYER_BLEND_OVERLAY,
        LAYER_BLEND_HARD_LIGHT,
        LAYER_BLEND_SOFT_LIGHT,
        LAYER_BLEND_DIFFERENCE,
        LAYER_BLEND_DODGE,
        LAYER_BLEND_BURN,
        LAYER_BLEND_EXCLUSION,
        LAYER_BLEND_TRUE_HUE,
        LAYER_BLEND_TRUE_SATURATION,
        LAYER_BLEND_TRUE_COLOR,
        LAYER_BLEND_TRUE_LIGHTNESS,
        LAYER_BLEND_ADJUST = 255,
    };

    /* Possible metrics used to measure resolution.
    */
    enum PSP_METRIC
    {
        PSP_METRIC_UNDEFINED = 0, // Metric unknown
        PSP_METRIC_INCH, // Resolution is in inches
        PSP_METRIC_CM, // Resolution is in centimeters
    };

    /* Creator application identifiers.
    */
    enum PSPCreatorAppID
    {
        PSP_CREATOR_APP_UNKNOWN = 0, // Creator application unknown
        PSP_CREATOR_APP_PAINT_SHOP_PRO, // Creator is Paint Shop Pro
    }

    /* Creator field types.
    */
    enum PSPCreatorFieldID
    {
        PSP_CRTR_FLD_TITLE = 0, // Image document title field
        PSP_CRTR_FLD_CRT_DATE, // Creation date field
        PSP_CRTR_FLD_MOD_DATE, // Modification date field
        PSP_CRTR_FLD_ARTIST, // Artist name field
        PSP_CRTR_FLD_CPYRGHT, // Copyright holder name field
        PSP_CRTR_FLD_DESC, // Image document description field
        PSP_CRTR_FLD_APP_ID, // Creating app id field
        PSP_CRTR_FLD_APP_VER, // Creating app version field
    }

    /* Extended data field types.
    */
    enum PSPExtendedDataID
    {
        PSP_XDATA_TRNS_INDEX = 0, // Transparency index field
        PSP_XDATA_GRID, // Image grid information
        PSP_XDATA_GUIDE // Image guide information
    }

    /* Grid units type.
    */
    enum PSPGridUnitsType
    {
        keGridUnitsPixels = 0, // Grid units is pixels
        keGridUnitsInches, // Grid units is inches
        keGridUnitsCentimeters // Grid units is centimeters
    }

    /* Graphic contents flags.
    */
    [Flags]
    enum PSPGraphicContents : uint
    {
        // Layer types
        keGCRasterLayers = 0x00000001, // At least one raster layer
        keGCVectorLayers = 0x00000002, // At least one vector layer
        keGCAdjustmentLayers = 0x00000004, // At least one adjustment layer
        // Additional attributes
        keGCThumbnail = 0x01000000, // Has a thumbnail
        keGCThumbnailTransparency = 0x02000000, // Thumbnail transp.
        keGCComposite = 0x04000000, // Has a composite image
        keGCCompositeTransparency = 0x08000000, // Composite transp.
        keGCFlatImage = 0x10000000, // Just a background
        keGCSelection = 0x20000000, // Has a selection
        keGCFloatingSelectionLayer = 0x40000000, // Has float. selection
        keGCAlphaChannels = 0x80000000 // Has alpha channel(s)
    }

}
