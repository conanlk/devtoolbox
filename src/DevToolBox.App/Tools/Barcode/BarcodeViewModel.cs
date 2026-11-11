using System.Text;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shared;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.Core.Tools.Barcode;

namespace DevToolBox.App.Tools.Barcode;

public sealed partial class BarcodeViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    private readonly IClipboardService _clipboard;
    private readonly IFileService _files;
    private readonly Debouncer _debouncer;
    private BarcodeMatrix? _matrix;

    public BarcodeViewModel(IClipboardService clipboard, IFileService files, TimeSpan? debounce = null)
    {
        _clipboard = clipboard;
        _files = files;
        _debouncer = new Debouncer(debounce ?? Debouncer.DefaultDelay);
        SelectedKind = Kinds[0];
        SelectedErrorCorrection = ErrorCorrections[1];
        Text = "https://example.com";
        Render();
    }

    public IReadOnlyList<Choice<BarcodeKind>> Kinds { get; } =
    [
        new("QR Code", BarcodeKind.QrCode),
        new("Code 128", BarcodeKind.Code128),
        new("EAN-13", BarcodeKind.Ean13),
        new("UPC-A", BarcodeKind.UpcA),
        new("Code 39", BarcodeKind.Code39),
    ];

    public IReadOnlyList<Choice<QrErrorCorrection>> ErrorCorrections { get; } =
    [
        new("L — 7%", QrErrorCorrection.L),
        new("M — 15%", QrErrorCorrection.M),
        new("Q — 25%", QrErrorCorrection.Q),
        new("H — 30%", QrErrorCorrection.H),
    ];

    [ObservableProperty]
    public partial Choice<BarcodeKind> SelectedKind { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Choice<QrErrorCorrection> SelectedErrorCorrection { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModuleSizeText))]
    public partial double ModuleSize { get; set; } = 6;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BarHeightText))]
    public partial double BarHeight { get; set; } = 100;

    /// <summary>"Module size: 6 px".</summary>
    public string ModuleSizeText => Localizer.Format(Strings.Barcode_ModuleSizeValue, ModuleSize);

    /// <summary>"Bar height: 100 px".</summary>
    public string BarHeightText => Localizer.Format(Strings.Barcode_BarHeightValue, BarHeight);

    [ObservableProperty]
    public partial bool ShowText { get; set; } = true;

    [ObservableProperty]
    public partial Bitmap? Preview { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string? Info { get; private set; }

    public bool IsQr => SelectedKind.Value == BarcodeKind.QrCode;

    public bool IsLinear => !IsQr;

    public bool HasBarcode => _matrix is not null;

    public string Hint => SelectedKind.Value switch
    {
        BarcodeKind.Ean13 => Strings.Barcode_HintEan13,
        BarcodeKind.UpcA => Strings.Barcode_HintUpcA,
        BarcodeKind.Code39 => Strings.Barcode_HintCode39,
        BarcodeKind.Code128 => Strings.Barcode_HintCode128,
        _ => Strings.Barcode_HintQr,
    };

    public void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
        Preview?.Dispose();
    }

    /// <summary>Re-encodes and re-renders immediately.</summary>
    public void Render()
    {
        if (SelectedKind is null || SelectedErrorCorrection is null)
        {
            return;
        }

        var result = BarcodeEncoder.Encode(new BarcodeRequest(SelectedKind.Value, Text, SelectedErrorCorrection.Value));
        _matrix = result.IsOk ? result.Value : null;
        ErrorMessage = result.Error?.Message;
        Info = _matrix is { } m
            ? m.Text != Text.Trim() && SelectedKind.Value is BarcodeKind.Ean13 or BarcodeKind.UpcA
                ? Localizer.Format(Strings.Barcode_EncodedAs, m.Text)
                : m.IsLinear ? Localizer.Format(Strings.Barcode_ModulesWide, m.Width) : Localizer.Format(Strings.Barcode_Modules, m.Width, m.Height)
            : null;

        var previous = Preview;
        Preview = _matrix is null ? null : BarcodeBitmapRenderer.Render(_matrix, RenderOptions);
        previous?.Dispose();
        OnPropertyChanged(nameof(HasBarcode));
    }

    private BarcodeRenderOptions RenderOptions => new((int)Math.Round(ModuleSize), (int)Math.Round(BarHeight), ShowText);

    [RelayCommand]
    private async Task SavePngAsync()
    {
        if (Preview is { } bitmap)
        {
            await _files.SaveFileAsync(Strings.Barcode_SavePngTitle, $"{FileStem()}.png", new FileTypeFilter(Strings.Common_PngImage, "png"),
                stream =>
                {
                    bitmap.Save(stream, PngBitmapEncoderOptions.Default);
                    return Task.CompletedTask;
                });
        }
    }

    [RelayCommand]
    private async Task SaveSvgAsync()
    {
        if (_matrix is { } matrix)
        {
            var svg = BarcodeSvgRenderer.Render(matrix, RenderOptions);
            await _files.SaveFileAsync(Strings.Barcode_SaveSvgTitle, $"{FileStem()}.svg", new FileTypeFilter(Strings.Common_SvgImage, "svg"),
                stream => stream.WriteAsync(Encoding.UTF8.GetBytes(svg)).AsTask());
        }
    }

    [RelayCommand]
    private async Task CopyImageAsync()
    {
        if (Preview is { } bitmap)
        {
            await _clipboard.SetBitmapAsync(bitmap);
        }
    }

    [RelayCommand]
    private async Task CopySvgAsync()
    {
        if (_matrix is { } matrix)
        {
            await _clipboard.SetTextAsync(BarcodeSvgRenderer.Render(matrix, RenderOptions));
        }
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(ModuleSizeText));
        OnPropertyChanged(nameof(BarHeightText));
        Render();
    }

    partial void OnSelectedKindChanged(Choice<BarcodeKind> value)
    {
        OnPropertyChanged(nameof(IsQr));
        OnPropertyChanged(nameof(IsLinear));
        OnPropertyChanged(nameof(Hint));
        Text = value.Value switch
        {
            BarcodeKind.Ean13 => "400638133393",
            BarcodeKind.UpcA => "03600029145",
            BarcodeKind.Code39 => "DEV-TOOL 39",
            BarcodeKind.Code128 => "DEV-TOOL-128",
            _ => "https://example.com",
        };
        Render();
    }

    partial void OnTextChanged(string value) => _debouncer.Run(Render);

    partial void OnSelectedErrorCorrectionChanged(Choice<QrErrorCorrection> value) => Render();

    partial void OnModuleSizeChanged(double value) => _debouncer.Run(Render);

    partial void OnBarHeightChanged(double value) => _debouncer.Run(Render);

    partial void OnShowTextChanged(bool value) => Render();

    private string FileStem() => SelectedKind.Value switch
    {
        BarcodeKind.QrCode => "qrcode",
        BarcodeKind.Code128 => "code128",
        BarcodeKind.Ean13 => "ean13",
        BarcodeKind.UpcA => "upca",
        _ => "code39",
    };

    public void AcceptInput(string text, string? intent)
    {
        SelectedKind = Kinds[0];
        Text = text.Trim();
        Render();
    }
}
