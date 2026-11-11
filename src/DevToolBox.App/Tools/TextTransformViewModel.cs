using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevToolBox.App.Localization;
using DevToolBox.App.Shared.Behaviors;
using DevToolBox.Core.Shared;

namespace DevToolBox.App.Tools;

/// <summary>
/// Shared behavior for "text in → text out" tools: debounced live transform, inline errors,
/// sample input and swapping output back into input.
/// </summary>
public abstract partial class TextTransformViewModel : ToolViewModelBase, IDisposable, IAcceptsInput
{
    private readonly Debouncer _debouncer;

    protected TextTransformViewModel(TimeSpan? debounce = null)
    {
        _debouncer = new Debouncer(debounce ?? Debouncer.DefaultDelay);
    }

    [ObservableProperty]
    public partial string Input { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Output { get; protected set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; protected set; }

    /// <summary>"12 B in → 16 chars out" for the status bar.</summary>
    [ObservableProperty]
    public partial string SizeText { get; private set; } = Strings.Common_Empty;

    protected abstract string SampleInput { get; }

    /// <summary>Recomputes the output immediately (option changes call this).</summary>
    public void Refresh()
    {
        if (Input.Length == 0)
        {
            Output = string.Empty;
            ErrorMessage = null;
            SizeText = Strings.Common_Empty;
            OnCleared();
            return;
        }

        var result = Transform(Input);
        Output = result.IsOk ? result.Value : string.Empty;
        ErrorMessage = result.Error?.Message;
        SizeText = Localizer.Format(Strings.Common_SizeInOut, Format(System.Text.Encoding.UTF8.GetByteCount(Input)), Output.Length);
    }

    private static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.##} MB"),
    };

    /// <summary>Re-runs the transform so status and Core error messages come back in the new language.</summary>
    protected override void OnLanguageChanged() => Refresh();

    public virtual void AcceptInput(string text, string? intent)
    {
        ApplyIntent(intent);
        Input = text;
        Refresh();
    }

    /// <summary>Sets the direction for an incoming "encode"/"decode" intent.</summary>
    protected virtual void ApplyIntent(string? intent)
    {
    }

    public virtual void Dispose()
    {
        StopLanguageUpdates();
        _debouncer.Dispose();
        GC.SuppressFinalize(this);
    }

    protected abstract Result<string> Transform(string input);

    /// <summary>Called before the output becomes the new input; flip the direction here.</summary>
    protected virtual void OnSwapping()
    {
    }

    protected virtual void OnCleared()
    {
    }

    [RelayCommand]
    private void LoadSample() => Input = SampleInput;

    [RelayCommand]
    private void Swap()
    {
        var output = Output;
        OnSwapping();
        Input = output;
        Refresh();
    }

    partial void OnInputChanged(string value) => _debouncer.Run(Refresh);
}
