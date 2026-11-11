using DevToolBox.Core.Tools.Barcode;

namespace DevToolBox.Core.Tests.Tools.Barcode;

public class BarcodeValidatorTests
{
    [Theory]
    [InlineData(BarcodeKind.Ean13, "400638133393", "4006381333931")]
    [InlineData(BarcodeKind.Ean13, "4006381333931", "4006381333931")]
    [InlineData(BarcodeKind.UpcA, "03600029145", "036000291452")]
    [InlineData(BarcodeKind.Code39, "hello-1", "HELLO-1")]
    [InlineData(BarcodeKind.Code128, "Abc-123!", "Abc-123!")]
    [InlineData(BarcodeKind.QrCode, "Xin chào 👋", "Xin chào 👋")]
    public void Normalize_AcceptsValidInput(BarcodeKind kind, string input, string expected) =>
        Assert.Equal(expected, BarcodeValidator.Normalize(kind, input).Value);

    [Theory]
    [InlineData(BarcodeKind.Ean13, "4006381333932", "barcode.checksum")]
    [InlineData(BarcodeKind.Ean13, "40063813", "barcode.length")]
    [InlineData(BarcodeKind.Ean13, "40063813339a", "barcode.digits")]
    [InlineData(BarcodeKind.UpcA, "036000291453", "barcode.checksum")]
    [InlineData(BarcodeKind.Code39, "a_b", "barcode.charset")]
    [InlineData(BarcodeKind.Code128, "café", "barcode.charset")]
    [InlineData(BarcodeKind.QrCode, "", "barcode.empty")]
    [InlineData(BarcodeKind.Code128, "   ", "barcode.empty")]
    public void Normalize_RejectsInvalidInput(BarcodeKind kind, string input, string code) =>
        Assert.Equal(code, BarcodeValidator.Normalize(kind, input).Error?.Code);

    [Fact]
    public void Normalize_QrTooLong_IsAnError() =>
        Assert.Equal("barcode.too-long", BarcodeValidator.Normalize(BarcodeKind.QrCode, new string('x', 3000)).Error?.Code);

    [Theory]
    [InlineData("400638133393", 1)]
    [InlineData("03600029145", 2)]
    [InlineData("0", 0)]
    public void CheckDigit_UsesGs1Mod10(string digits, int expected) => Assert.Equal(expected, BarcodeValidator.CheckDigit(digits));
}

public class BarcodeEncoderTests
{
    [Theory]
    [InlineData(BarcodeKind.QrCode, "https://example.com")]
    [InlineData(BarcodeKind.Code128, "DEV-TOOL-128")]
    [InlineData(BarcodeKind.Ean13, "400638133393")]
    [InlineData(BarcodeKind.UpcA, "03600029145")]
    [InlineData(BarcodeKind.Code39, "CODE 39")]
    public void Encode_ProducesMatrixWithQuietZone(BarcodeKind kind, string text)
    {
        var matrix = BarcodeEncoder.Encode(new BarcodeRequest(kind, text, QuietZoneModules: 4)).Value;

        Assert.Equal(kind != BarcodeKind.QrCode, matrix.IsLinear);
        Assert.True(matrix.Width > 20);
        Assert.False(matrix[0, 0]);
        Assert.False(matrix[matrix.Width - 1, matrix.Height - 1]);
        Assert.Contains(Enumerable.Range(0, matrix.Width), x => matrix[x, matrix.Height / 2]);
    }

    [Fact]
    public void Encode_QrSizeGrowsWithErrorCorrection()
    {
        var low = BarcodeEncoder.Encode(new BarcodeRequest(BarcodeKind.QrCode, new string('A', 100), QrErrorCorrection.L)).Value;
        var high = BarcodeEncoder.Encode(new BarcodeRequest(BarcodeKind.QrCode, new string('A', 100), QrErrorCorrection.H)).Value;

        Assert.True(high.Width > low.Width);
        Assert.Equal(low.Width, low.Height);
    }

    [Fact]
    public void Encode_ReturnsValidatedText() =>
        Assert.Equal("4006381333931", BarcodeEncoder.Encode(new BarcodeRequest(BarcodeKind.Ean13, "400638133393")).Value.Text);

    [Fact]
    public void Encode_InvalidInput_PropagatesValidationError() =>
        Assert.Equal("barcode.checksum", BarcodeEncoder.Encode(new BarcodeRequest(BarcodeKind.Ean13, "4006381333932")).Error?.Code);

    [Fact]
    public void SvgRenderer_ProducesWellFormedSvgWithText()
    {
        var matrix = BarcodeEncoder.Encode(new BarcodeRequest(BarcodeKind.Code128, "A<&>B")).Value;

        var svg = BarcodeSvgRenderer.Render(matrix, new BarcodeRenderOptions(ModuleSize: 2, BarHeight: 50, ShowText: true));

        var doc = System.Xml.Linq.XDocument.Parse(svg);
        Assert.Equal("svg", doc.Root!.Name.LocalName);
        Assert.Contains("A&lt;&amp;&gt;B", svg, StringComparison.Ordinal);
        Assert.Equal((matrix.Width * 2).ToString(System.Globalization.CultureInfo.InvariantCulture), doc.Root.Attribute("width")!.Value);
    }

    [Fact]
    public void Layout_QrHasNoTextAndSquareSize()
    {
        var matrix = BarcodeEncoder.Encode(new BarcodeRequest(BarcodeKind.QrCode, "x")).Value;

        var layout = BarcodeLayout.Compute(matrix, new BarcodeRenderOptions(ModuleSize: 3, ShowText: true));

        Assert.Equal(0, layout.TextHeight);
        Assert.Equal(layout.Width, layout.Height);
    }
}
