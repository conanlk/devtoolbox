using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;
using ZXing;
using ZXing.QrCode.Internal;

namespace DevToolBox.Core.Tools.Barcode;

/// <summary>Encodes text into a module matrix using ZXing.Net (no image rendering here).</summary>
public static class BarcodeEncoder
{
    public static Result<BarcodeMatrix> Encode(BarcodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = BarcodeValidator.Normalize(request.Kind, request.Text);
        if (!text.IsOk)
        {
            return text.Error;
        }

        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = Math.Clamp(request.QuietZoneModules, 0, 20),
        };
        if (request.Kind == BarcodeKind.QrCode)
        {
            hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
            hints[EncodeHintType.ERROR_CORRECTION] = request.ErrorCorrection switch
            {
                QrErrorCorrection.L => ErrorCorrectionLevel.L,
                QrErrorCorrection.Q => ErrorCorrectionLevel.Q,
                QrErrorCorrection.H => ErrorCorrectionLevel.H,
                _ => ErrorCorrectionLevel.M,
            };
        }

        try
        {
            // Width/height 1 → ZXing returns the minimal size: one pixel per module.
            var bits = new MultiFormatWriter().encode(text.Value, ToZxing(request.Kind), 1, 1, hints);
            var isLinear = request.Kind != BarcodeKind.QrCode;
            var height = isLinear ? 1 : bits.Height;
            var modules = new bool[bits.Width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < bits.Width; x++)
                {
                    modules[(y * bits.Width) + x] = bits[x, y];
                }
            }

            return new BarcodeMatrix(bits.Width, height, modules, text.Value, isLinear);
        }
        catch (Exception ex) when (ex is WriterException or ArgumentException)
        {
            return new Error("barcode.encode", CoreLocalization.Format(CoreStrings.Barcode_Encode, ex.Message));
        }
    }

    private static BarcodeFormat ToZxing(BarcodeKind kind) => kind switch
    {
        BarcodeKind.QrCode => BarcodeFormat.QR_CODE,
        BarcodeKind.Code128 => BarcodeFormat.CODE_128,
        BarcodeKind.Ean13 => BarcodeFormat.EAN_13,
        BarcodeKind.UpcA => BarcodeFormat.UPC_A,
        BarcodeKind.Code39 => BarcodeFormat.CODE_39,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
