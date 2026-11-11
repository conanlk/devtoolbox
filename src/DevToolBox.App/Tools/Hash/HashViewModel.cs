using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Platform;
using DevToolBox.App.Shared;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.Core.Shared;
using DevToolBox.Core.Tools.Hash;

namespace DevToolBox.App.Tools.Hash;

public enum HashSource
{
    Text,
    File,
}

public sealed partial class HashRowViewModel(HashAlgorithmKind algorithm) : ObservableObject
{
    public HashAlgorithmKind Algorithm { get; } = algorithm;

    public string Name { get; } = HashAlgorithms.DisplayName(algorithm);

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsMatch { get; set; }
}

public sealed partial class HashViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    private readonly IFileService _files;
    private readonly Debouncer _debouncer;
    private readonly LatestTaskRunner _fileRunner = new();
    private readonly bool _hasUnsupported;
    private IReadOnlyDictionary<HashAlgorithmKind, byte[]> _hashes = new Dictionary<HashAlgorithmKind, byte[]>();

    public HashViewModel(IFileService files, TimeSpan? debounce = null)
    {
        _files = files;
        _debouncer = new Debouncer(debounce ?? Debouncer.DefaultDelay);
        SelectedFormat = Formats[0];
        foreach (var algorithm in HashAlgorithms.All.Where(HashAlgorithms.IsSupported))
        {
            Rows.Add(new HashRowViewModel(algorithm));
        }

        _hasUnsupported = HashAlgorithms.All.Any(a => !HashAlgorithms.IsSupported(a));
    }

    public ObservableCollection<HashRowViewModel> Rows { get; } = [];

    public string? UnsupportedNote => _hasUnsupported ? Strings.Hash_Sha3Unsupported : null;

    public IReadOnlyList<Choice<HashOutputFormat>> Formats { get; } =
    [
        new(() => Strings.Hash_HexLower, HashOutputFormat.HexLower),
        new(() => Strings.Hash_HexUpper, HashOutputFormat.HexUpper),
        new("Base64", HashOutputFormat.Base64),
    ];

    [ObservableProperty]
    public partial HashSource Source { get; set; } = HashSource.Text;

    [ObservableProperty]
    public partial string TextInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Choice<HashOutputFormat> SelectedFormat { get; set; }

    [ObservableProperty]
    public partial bool UseHmac { get; set; }

    [ObservableProperty]
    public partial string HmacKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExpectedHash { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CompareResult { get; private set; }

    [ObservableProperty]
    public partial bool CompareMatched { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>"Text · 18 B (UTF-8) · 6 algorithms" for the status bar.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = Strings.Hash_NoInput;

    // ----- File -----

    [ObservableProperty]
    public partial string? FileName { get; private set; }

    [ObservableProperty]
    public partial string? FileDetails { get; private set; }

    [ObservableProperty]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    public partial bool IsHashing { get; private set; }

    public Task LastFileRun { get; private set; } = Task.CompletedTask;

    private PickedFile? CurrentFile { get; set; }

    public void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
        _fileRunner.Dispose();
    }

    /// <summary>Hashes the text input immediately.</summary>
    public void HashText()
    {
        if (Source != HashSource.Text)
        {
            return;
        }

        var result = HashCalculator.ComputeText(TextInput, Rows.Select(r => r.Algorithm), HmacKeyOrNull);
        ShowResult(result);
    }

    [RelayCommand]
    public Task HashFileAsync(PickedFile? file)
    {
        if (file is null)
        {
            return Task.CompletedTask;
        }

        Source = HashSource.File;
        CurrentFile = file;
        FileName = file.Name;
        Progress = 0;
        IsHashing = true;
        ErrorMessage = null;
        ClearValues();

        var total = file.Size ?? 0;
        FileDetails = total > 0 ? FormatBytes(total) : null;
        var progress = new Progress<long>(done => Progress = total > 0 ? done * 100.0 / total : 0);
        var algorithms = Rows.Select(r => r.Algorithm).ToList();
        var key = HmacKeyOrNull;

        return LastFileRun = _fileRunner.RunAsync(
            async token =>
            {
                await using var stream = await file.OpenReadAsync().ConfigureAwait(false);
                return await HashCalculator.ComputeStreamAsync(stream, algorithms, key, progress, token).ConfigureAwait(false);
            },
            result =>
            {
                IsHashing = false;
                Progress = 100;
                ShowResult(result);
            },
            ex =>
            {
                IsHashing = false;
                ErrorMessage = Localizer.Format(Strings.Hash_ReadFailed, ex.Message);
            });
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var file = await _files.OpenFileAsync(Strings.Hash_ChooseFileTitle);
        await HashFileAsync(file);
    }

    [RelayCommand]
    private void CancelHashing()
    {
        _fileRunner.Cancel();
        IsHashing = false;
        Progress = 0;
        FileDetails = Strings.Hash_Cancelled;
    }

    /// <summary>Re-hashes text (cheap) for localized errors; file results are only relabeled, never re-read.</summary>
    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(UnsupportedNote));
        if (Source == HashSource.Text)
        {
            HashText();
        }
        else
        {
            UpdateStatus();
            FormatValues();
        }
    }

    partial void OnTextInputChanged(string value) => _debouncer.Run(HashText);

    partial void OnSourceChanged(HashSource value)
    {
        ClearValues();
        if (value == HashSource.Text)
        {
            HashText();
        }
    }

    partial void OnSelectedFormatChanged(Choice<HashOutputFormat> value) => FormatValues();

    partial void OnUseHmacChanged(bool value) => Recompute();

    partial void OnHmacKeyChanged(string value)
    {
        if (UseHmac)
        {
            _debouncer.Run(Recompute);
        }
    }

    partial void OnExpectedHashChanged(string value) => Compare();

    private string? HmacKeyOrNull => UseHmac ? HmacKey : null;

    private void Recompute()
    {
        if (Source == HashSource.Text)
        {
            HashText();
        }
        else if (CurrentFile is not null)
        {
            _ = HashFileAsync(CurrentFile);
        }
    }

    private void ShowResult(Result<IReadOnlyDictionary<HashAlgorithmKind, byte[]>> result)
    {
        ErrorMessage = result.Error?.Message;
        _hashes = result.IsOk ? result.Value : new Dictionary<HashAlgorithmKind, byte[]>();
        UpdateStatus();
        FormatValues();
    }

    private void UpdateStatus()
    {
        var source = Source == HashSource.Text
            ? Localizer.Format(Strings.Hash_StatusText, FormatBytes(System.Text.Encoding.UTF8.GetByteCount(TextInput)))
            : Localizer.Format(Strings.Hash_StatusFile, FileName, FileDetails);
        var algorithms = Localizer.Format(_hashes.Count == 1 ? Strings.Hash_Algorithm : Strings.Hash_Algorithms, _hashes.Count);
        StatusText = $"{source} · {algorithms}{(UseHmac ? " · HMAC" : string.Empty)}";
    }

    private void FormatValues()
    {
        foreach (var row in Rows)
        {
            row.Value = _hashes.TryGetValue(row.Algorithm, out var hash)
                ? HashCalculator.Format(hash, SelectedFormat.Value)
                : UseHmac && row.Algorithm == HashAlgorithmKind.Crc32 ? Strings.Hash_NoHmacForCrc : string.Empty;
        }

        Compare();
    }

    private void ClearValues()
    {
        _hashes = new Dictionary<HashAlgorithmKind, byte[]>();
        FormatValues();
    }

    private void Compare()
    {
        var matched = Rows.Where(row => _hashes.TryGetValue(row.Algorithm, out var hash) && HashCalculator.Matches(hash, ExpectedHash)).ToList();
        foreach (var row in Rows)
        {
            row.IsMatch = matched.Contains(row);
        }

        CompareMatched = matched.Count > 0;
        CompareResult = string.IsNullOrWhiteSpace(ExpectedHash) || _hashes.Count == 0
            ? null
            : CompareMatched
                ? Localizer.Format(Strings.Hash_Matches, string.Join(", ", matched.Select(r => r.Name)))
                : Strings.Hash_NoMatch;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        < 1024L * 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.#} MB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024 * 1024):0.##} GB"),
    };

    /// <summary>"compare" puts a pasted hash into the compare field and switches to file hashing.</summary>
    public void AcceptInput(string text, string? intent)
    {
        if (intent == "compare")
        {
            ExpectedHash = text.Trim();
            Source = HashSource.File;
            return;
        }

        Source = HashSource.Text;
        TextInput = text;
        HashText();
    }
}
