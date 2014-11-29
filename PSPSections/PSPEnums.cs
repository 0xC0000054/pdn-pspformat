// PSP File Format Specification is Copyright (C) 2000 Jasc Software, Inc.

using System;

namespace PaintShopProFiletype.PSPSections
{
    /* Block identifiers.
    */
    enum PSPBlockID
    {
        ImageAttributes = 0, // General Image Attributes Block (main)
        Creator, // Creator Data Block (main)
        ColorPalette, // Color Palette Block (main and sub)
        LayerStart, // Layer Bank Block (main)
        Layer, // Layer Block (sub)
        Channel, // Channel Block (sub)
        Selection, // Selection Block (main)
        AlphaBank, // Alpha Bank Block (main)
        AlphaChannel,// Alpha Channel Block (sub)
        CompositeImage, // Composite Image Block (sub)
        ExtendedData,// Extended Data Block (main)
        PictureTube, // Picture Tube Data Block (main)
        AdjustmentLayerExtension, // Adjustment Layer Block (sub)
        VectorLayerExtension, // Vector Layer Block (sub)
        VectorShape, // Vector Shape Block (sub)
        PaintStyle, // Paint Style Block (sub)
        CompositeImageBank, // Composite Image Bank (main)
        CompositeImageAttributes, // Composite Image Attr. (sub)
        JPEGImage, // JPEG Image Block (sub)
        LineStyle, // Line Style Block (sub)
        TableBank, // Table Bank Block (main)
        Table, // Table Block (sub)
        Paper, // Vector Table Paper Block (sub)
        Pattern, // Vector Table Pattern Block (sub)
        GroupLayerExtension, // Group Layer Block (sub)
        MaskLayerExtension, // Mask Layer Block (sub)
        BrushData, // Brush Data Block (main)
    }

    /* Bitmap types.
    */
    enum PSPDIBType
    {
        Image = 0, // Layer color bitmap
        TransparencyMask, // Layer transparency mask bitmap
        UserMask, // Layer user mask bitmap
        Selection, // Selection mask bitmap
        AlphaMask, // Alpha channel mask bitmap
        Thumbnail, // Thumbnail bitmap
        ThumbnailTransparencyMask, // Thumbnail transparency mask
        AdjustmentLayer, // Adjustment layer bitmap
        Composite, // Composite image bitmap
        CompositeTransparencyMask, // Composite image transparency
        Paper, // Paper bitmap
        Pattern, // Pattern bitmap
        PatternTransparencyMask, // Pattern transparency mask
    };
    /* Type of image in the composite image bank block.
    */
    enum PSPCompositeImageType
    {
        Composite = 0, // Composite Image
        Thumbnail, // Thumbnail Image
    };
    /* Channel types.
    */
    enum PSPChannelType
    {
        Composite = 0, // Channel of single channel bitmap
        Red, // Red channel of 24-bit bitmap
        Green, // Green channel of 24-bit bitmap
        Blue, // Blue channel of 24-bit bitmap
    };
    /* Possible types of compression.
    */
    enum PSPCompression
    {
        None = 0, // No compression
        RLE, // RLE compression
        LZ77, // LZ77 compression
        JPEG // JPEG compression (only used by thumbnail and composite image)
    };
    
    /* Layer types.
    */
    enum PSPLayerType
    {
        Undefined = 0, // Undefined layer type
        Raster, // Standard raster layer
        FloatingRasterSelection, // Floating selection (raster)
        Vector, // Vector layer
        Adjustment, // Adjustment layer
    }

    /* Layer flags.
    */
    [Flags]
    enum PSPLayerProperties
    {
        None = 0,
        Visible = 1, // Layer is visible
        MaskPresence = 2 // Layer has a mask
    }

    /* Blend modes.
    */
    enum PSPBlendModes
    {
        Normal,
        Darken,
        Lighten,
        LegacyHue,
        LegacySaturation,
        Color,
        LegacyLuminosity,
        Multiply,
        Screen,
        Dissolve,
        Overlay,
        HardLight,
        SoftLight,
        Difference,
        Dodge,
        Burn,
        Exclusion,
        TrueHue,
        TrueSaturation,
        TrueColor,
        TrueLightness,
        Adjust = 255,
    };

    /* Possible metrics used to measure resolution.
    */
    enum ResolutionMetric
    {
        Undefined = 0, // Metric unknown
        Inch, // Resolution is in inches
        Centimeter, // Resolution is in centimeters
    };

    /* Creator application identifiers.
    */
    enum PSPCreatorAppID
    {
        Unknown = 0, // Creator application unknown
        PaintShopPro, // Creator is Paint Shop Pro
    }
    /* Creator field types.
    */
    enum PSPCreatorFieldID
    {
        Title = 0, // Image document title field
        CreateDate, // Creation date field
        ModifiedDate, // Modification date field
        Artist, // Artist name field
        Copyright, // Copyright holder name field
        Description, // Image document description field
        ApplicationID, // Creating app id field
        ApplicationVersion, // Creating app version field
    }
    /* Extended data field types.
    */
    enum PSPExtendedDataID
    {
        TransparencyIndex = 0, // Transparency index field
        Grid, // Image grid information
        Guide, // Image guide information
    }
    /* Grid units type.
    */
    enum PSPGridUnitsType
    {
        Pixels = 0, // Grid units is pixels
        Inches, // Grid units is inches
        Centimeters // Grid units is centimeters
    }

    /* Graphic contents flags.
    */
    [Flags]
    enum PSPGraphicContents : uint
    {
        // Layer types
        RasterLayers = 0x00000001, // At least one raster layer
        VectorLayers = 0x00000002, // At least one vector layer
        AdjustmentLayers = 0x00000004, // At least one adjust. layer
        // Additional attributes
        Thumbnail = 0x01000000, // Has a thumbnail
        ThumbnailTransparency = 0x02000000, // Thumbnail transp.
        Composite = 0x04000000, // Has a composite image
        CompositeTransparency = 0x08000000, // Composite transp.
        FlatImage = 0x10000000, // Just a background
        Selection = 0x20000000, // Has a selection
        FloatingSelectionLayer = 0x40000000, // Has float. selection
        AlphaChannels = 0x80000000 // Has alpha channel(s)
    }

}
