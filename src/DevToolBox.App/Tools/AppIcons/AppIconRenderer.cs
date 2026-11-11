using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DevToolBox.Core.Tools.AppIcons;

namespace DevToolBox.App.Tools.AppIcons;

/// <summary>How a launcher shows an icon, for the "Preview by mask" row.</summary>
public enum IconMask
{
    /// <summary>iOS: continuous rounded rectangle (~22.4% corner radius).</summary>
    IosRoundedRect,

    /// <summary>Android adaptive icon under a circle mask (Pixel).</summary>
    AdaptiveCircle,

    /// <summary>Android adaptive icon under a squircle mask (Samsung One UI).</summary>
    AdaptiveSquircle,

    /// <summary>Android adaptive icon under a rounded-square mask.</summary>
    AdaptiveRoundedSquare,

    /// <summary>Android adaptive icon under a teardrop mask.</summary>
    AdaptiveTeardrop,
}

/// <summary>Draws icons from the (cropped) source with high-quality downscaling.</summary>
public static class AppIconRenderer
{
    /// <summary>The visible part of an adaptive icon is the centre 72dp of its 108dp layers.</summary>
    private const double AdaptiveVisibleFraction = 72.0 / 108.0;

    /// <summary>Share of the 108dp adaptive foreground layer the artwork fills (keeps it inside the launcher masks).</summary>
    private const double AdaptiveArtworkScale = 0.72;

    // The app icon's own colours (build/icons/app/devtoolbox-icon.svg); it's artwork, so it doesn't follow the theme.
    private const double AppIconUnits = 1024;
    private const string AppIconBraces = "M400 290C330 290 330 330 330 390V460C330 500 310 512 270 512C310 512 330 524 330 564V634C330 694 330 734 400 734M624 290C694 290 694 330 694 390V460C694 500 714 512 754 512C714 512 694 524 694 564V634C694 694 694 734 624 734";
    private static readonly Color AppIconGround = Color.Parse("#161A22");
    private static readonly Color AppIconBracesColor = Color.Parse("#E7E9EE");
    private static readonly Color AppIconCursorColor = Color.Parse("#F2B544");

    /// <param name="padding">Space added around the artwork on each side, in percent of the artwork size (0–30).</param>
    public static Bitmap Render(Bitmap source, IconSourcePlan plan, IconSpec spec, Color background, double padding)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(spec);
        var size = spec.PixelSize;
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using var context = bitmap.CreateDrawingContext(true);
        using var quality = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality });
        var full = new Rect(0, 0, size, size);
        var crop = Crop(plan);
        var scale = ArtworkScale(padding);

        switch (spec.Shape)
        {
            case IconShape.Round:
                using (context.PushGeometryClip(new EllipseGeometry(full)))
                {
                    context.FillRectangle(new SolidColorBrush(background), full);
                    context.DrawImage(source, crop, Centered(full, scale));
                }

                break;
            case IconShape.AdaptiveForeground:
                // Transparent layer; the background colour comes from the XML background layer.
                context.DrawImage(source, crop, Centered(full, AdaptiveArtworkScale * scale));
                break;
            default:
                context.FillRectangle(new SolidColorBrush(background), full);
                context.DrawImage(source, crop, Centered(full, scale));
                break;
        }

        return bitmap;
    }

    /// <summary>A home-screen style preview of the icon under a launcher mask.</summary>
    public static Bitmap RenderPreview(Bitmap source, IconSourcePlan plan, IconMask mask, Color background, double padding, int size)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(plan);
        var scale = ArtworkScale(padding);
        return RenderMasked(mask, size, new SolidColorBrush(background), (context, artwork) =>
            context.DrawImage(source, Crop(plan), Centered(artwork, scale)));
    }

    /// <summary>The DevToolBox app icon (design concept A) under a launcher mask, shown before an image is loaded.</summary>
    public static Bitmap RenderDefaultPreview(IconMask mask, int size) =>
        RenderMasked(mask, size, new SolidColorBrush(AppIconGround), DrawAppIconGlyph);

    /// <summary>The cropped source as-is (no background, padding or mask), for the source card.</summary>
    public static Bitmap RenderThumbnail(Bitmap source, IconSourcePlan plan, int size)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(plan);
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using var context = bitmap.CreateDrawingContext(true);
        using var quality = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality });
        context.DrawImage(source, Crop(plan), new Rect(0, 0, size, size));
        return bitmap;
    }

    public static byte[] ToPng(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    /// <summary>True when any pixel is not fully opaque.</summary>
    public static bool HasTransparency(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.AlphaFormat is null or Avalonia.Platform.AlphaFormat.Opaque)
        {
            return false;
        }

        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var length = stride * size.Height;
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), buffer, length, stride);
            var pixels = new byte[length];
            Marshal.Copy(buffer, pixels, 0, length);

            // Alpha is the 4th byte for both BGRA8888 and RGBA8888.
            for (var i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 255)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Clips to the mask, fills the background and lets <paramref name="drawArtwork"/> paint into the artwork area.</summary>
    private static Bitmap RenderMasked(IconMask mask, int size, IBrush background, Action<DrawingContext, Rect> drawArtwork)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using var context = bitmap.CreateDrawingContext(true);
        using var quality = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality });
        var full = new Rect(0, 0, size, size);

        using (context.PushClip(MaskShape(mask, full)))
        {
            context.FillRectangle(background, full);

            // Adaptive previews show the visible 72dp window of the 108dp layers.
            var artwork = mask == IconMask.IosRoundedRect
                ? full
                : Centered(full.Inflate(size * ((1 / AdaptiveVisibleFraction) - 1) / 2), AdaptiveArtworkScale);
            drawArtwork(context, artwork);
        }

        return bitmap;
    }

    /// <summary>Braces and amber cursor of <c>build/icons/app/devtoolbox-icon.svg</c>, drawn into a 1024-unit square.</summary>
    private static void DrawAppIconGlyph(DrawingContext context, Rect area)
    {
        var scale = area.Width / AppIconUnits;
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(area.X, area.Y));
        var braces = StreamGeometry.Parse(AppIconBraces);
        var pen = new Pen(new SolidColorBrush(AppIconBracesColor), 72, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, pen, braces);
        context.DrawRectangle(new SolidColorBrush(AppIconCursorColor), null, new RoundedRect(new Rect(447, 447, 130, 130), 24));
    }

    private static Rect Crop(IconSourcePlan plan) => new(plan.CropX, plan.CropY, plan.CropSize, plan.CropSize);

    /// <summary>Padding is added around the artwork, so it shrinks to 1 / (1 + 2 × padding) of the icon.</summary>
    private static double ArtworkScale(double padding) => 1 / (1 + (2 * Math.Clamp(padding, 0, 30) / 100));

    private static RoundedRect MaskShape(IconMask mask, Rect full)
    {
        var size = full.Width;
        return mask switch
        {
            IconMask.IosRoundedRect => new RoundedRect(full, new CornerRadius(size * 0.2237)),
            IconMask.AdaptiveCircle => new RoundedRect(full, new CornerRadius(size / 2)),
            IconMask.AdaptiveSquircle => new RoundedRect(full, new CornerRadius(size * 0.32)),
            IconMask.AdaptiveRoundedSquare => new RoundedRect(full, new CornerRadius(size * 0.18)),
            // Teardrop: round everywhere except the bottom-left corner.
            _ => new RoundedRect(full, new CornerRadius(size / 2, size / 2, size / 2, size * 0.12)),
        };
    }

    private static Rect Centered(Rect area, double scale)
    {
        var side = area.Width * scale;
        return new Rect(area.X + ((area.Width - side) / 2), area.Y + ((area.Height - side) / 2), side, side);
    }
}
