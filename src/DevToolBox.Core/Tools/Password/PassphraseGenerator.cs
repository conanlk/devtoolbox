using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.Password;

public sealed record PassphraseOptions(
    int Words = 5,
    string Separator = " ",
    bool Capitalize = false,
    bool IncludeNumber = false,
    int Count = 1);

/// <summary>
/// Diceware-style passphrases from the EFF Large Wordlist (7,776 words ≈ 12.9 bits each), picked uniformly
/// with the OS CSPRNG.
/// </summary>
public static class PassphraseGenerator
{
    public const int MinWords = 3;
    public const int MaxWords = 12;
    public const int MaxSeparatorLength = 3;

    private static readonly Lazy<string[]> Wordlist = new(LoadWordlist);

    public static IReadOnlyList<string> Words => Wordlist.Value;

    public static Result<PasswordBatch> Generate(PassphraseOptions options, Func<int, int>? nextInt = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        nextInt ??= RandomNumberGenerator.GetInt32;

        if (options.Words is < MinWords or > MaxWords)
        {
            return new Error("passphrase.words", CoreLocalization.Format(CoreStrings.Passphrase_Words, MinWords, MaxWords));
        }

        if (options.Count is < 1 or > PasswordGenerator.MaxCount)
        {
            return new Error("password.count", CoreLocalization.Format(CoreStrings.Password_Count, PasswordGenerator.MaxCount));
        }

        var separator = options.Separator ?? string.Empty;
        if (separator.Length > MaxSeparatorLength)
        {
            return new Error("passphrase.separator", CoreLocalization.Format(CoreStrings.Passphrase_Separator, MaxSeparatorLength));
        }

        var words = Wordlist.Value;
        var phrases = new string[options.Count];
        for (var i = 0; i < phrases.Length; i++)
        {
            var picked = new string[options.Words];
            for (var w = 0; w < picked.Length; w++)
            {
                var word = words[nextInt(words.Length)];
                picked[w] = options.Capitalize ? char.ToUpperInvariant(word[0]) + word[1..] : word;
            }

            if (options.IncludeNumber)
            {
                var target = nextInt(picked.Length);
                picked[target] += nextInt(10).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            phrases[i] = string.Join(separator, picked);
        }

        var entropy = EntropyBits(options.Words, options.IncludeNumber);
        return new PasswordBatch(phrases, words.Length, entropy, PasswordGenerator.Rate(entropy));
    }

    /// <summary>words × log2(7776), plus log2(10 × words) when a digit is added to a random word.</summary>
    public static double EntropyBits(int words, bool includeNumber) =>
        (words * Math.Log2(Wordlist.Value.Length)) + (includeNumber ? Math.Log2(10.0 * words) : 0);

    private static string[] LoadWordlist()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DevToolBox.Core.eff_large_wordlist.txt")
                           ?? throw new InvalidOperationException("Embedded wordlist is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
