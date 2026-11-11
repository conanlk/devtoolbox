using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DevToolBox.Core.Tools.Barcode;

namespace DevToolBox.App.Tools.Barcode;

/// <summary>Draws a barcode matrix to a bitmap (preview, PNG export, clipboard), sharing layout with the SVG renderer.</summary>
public static class BarcodeBitmapRenderer
{
    public static Bitmap Render(BarcodeMatrix matrix, BarcodeRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var layout = BarcodeLayout.Compute(matrix, options);
        var bitmap = new RenderTargetBitmap(new PixelSize(layout.Width, layout.Height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, layout.Width, layout.Height));
            var m = layout.ModuleSize;
            var rowHeight = matrix.IsLinear ? layout.SymbolHeight : m;
            for (var y = 0; y < matrix.Height; y++)
            {
                var x = 0;
                while (x < matrix.Width)
                {
                    if (!matrix[x, y])
                    {
                        x++;
                        continue;
                    }

                    var start = x;
                    while (x < matrix.Width && matrix[x, y])
                    {
                        x++;
                    }

                    context.FillRectangle(Brushes.Black, new Rect(start * m, y * rowHeight, (x - start) * m, rowHeight));
                }
            }

            if (layout.TextHeight > 0)
            {
                var text = new FormattedText(
                    matrix.Text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Parse("Menlo, Consolas, DejaVu Sans Mono, monospace")),
                    layout.FontSize,
                    Brushes.Black);
                context.DrawText(text, new Point((layout.Width - text.Width) / 2, layout.SymbolHeight + ((layout.TextHeight - text.Height) / 2)));
            }
        }

        return bitmap;
    }
}
