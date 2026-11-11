namespace DevToolBox.Core.Tools.Barcode;

public enum BarcodeKind
{
    QrCode,
    Code128,
    Ean13,
    UpcA,
    Code39,
}

public enum QrErrorCorrection
{
    /// <summary>~7% of data can be restored.</summary>
    L,

    /// <summary>~15%.</summary>
    M,

    /// <summary>~25%.</summary>
    Q,

    /// <summary>~30%.</summary>
    H,
}

public sealed record BarcodeRequest(
    BarcodeKind Kind,
    string Text,
    QrErrorCorrection ErrorCorrection = QrErrorCorrection.M,
    int QuietZoneModules = 4);

/// <summary>
/// Encoded symbol as a grid of modules (true = dark), including the quiet zone.
/// 1D barcodes have a single row that is stretched vertically when rendered.
/// </summary>
public sealed class BarcodeMatrix(int width, int height, bool[] modules, string text, bool isLinear)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>The validated text actually encoded (e.g. with the computed EAN/UPC check digit).</summary>
    public string Text { get; } = text;

    public bool IsLinear { get; } = isLinear;

    public bool this[int x, int y] => modules[(y * Width) + x];
}

public sealed record BarcodeRenderOptions(int ModuleSize = 4, int BarHeight = 80, bool ShowText = true)
{
    public const int MinModuleSize = 1;
    public const int MaxModuleSize = 20;
}

/// <summary>Pixel geometry shared by the SVG and PNG renderers.</summary>
public sealed record BarcodeLayout(int Width, int Height, int ModuleSize, int SymbolHeight, int TextHeight, double FontSize)
{
    public static BarcodeLayout Compute(BarcodeMatrix matrix, BarcodeRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(options);
        var module = Math.Clamp(options.ModuleSize, BarcodeRenderOptions.MinModuleSize, BarcodeRenderOptions.MaxModuleSize);
        var width = matrix.Width * module;
        var symbolHeight = matrix.IsLinear ? Math.Max(options.BarHeight, module * 10) : matrix.Height * module;
        var showText = options.ShowText && matrix.IsLinear;
        var fontSize = Math.Max(10, module * 4);
        var textHeight = showText ? (int)Math.Ceiling(fontSize * 1.5) : 0;
        return new BarcodeLayout(width, symbolHeight + textHeight, module, symbolHeight, textHeight, fontSize);
    }
}
