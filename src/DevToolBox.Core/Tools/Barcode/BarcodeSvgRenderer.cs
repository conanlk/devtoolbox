using System.Globalization;
using System.Security;
using System.Text;

namespace DevToolBox.Core.Tools.Barcode;

/// <summary>Renders a <see cref="BarcodeMatrix"/> as a crisp, scalable SVG document.</summary>
public static class BarcodeSvgRenderer
{
    public static string Render(BarcodeMatrix matrix, BarcodeRenderOptions options)
    {
        var layout = BarcodeLayout.Compute(matrix, options);
        var m = layout.ModuleSize;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{layout.Width}\" height=\"{layout.Height}\" viewBox=\"0 0 {layout.Width} {layout.Height}\" shape-rendering=\"crispEdges\">");
        svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{layout.Width}\" height=\"{layout.Height}\" fill=\"#ffffff\"/>");
        svg.Append("<path fill=\"#000000\" d=\"");

        var rowHeight = matrix.IsLinear ? layout.SymbolHeight : m;
        for (var y = 0; y < matrix.Height; y++)
        {
            // Merge horizontal runs of dark modules into one rectangle each.
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

                svg.Append(CultureInfo.InvariantCulture, $"M{start * m} {y * rowHeight}h{(x - start) * m}v{rowHeight}h{-(x - start) * m}z");
            }
        }

        svg.Append("\"/>");
        if (layout.TextHeight > 0)
        {
            var baseline = layout.SymbolHeight + (layout.TextHeight * 0.8);
            svg.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{layout.Width / 2.0}\" y=\"{baseline}\" font-family=\"monospace\" font-size=\"{layout.FontSize}\" text-anchor=\"middle\" fill=\"#000000\">{SecurityElement.Escape(matrix.Text)}</text>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }
}
