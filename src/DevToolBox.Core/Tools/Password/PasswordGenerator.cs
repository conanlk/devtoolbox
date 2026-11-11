using System.Security.Cryptography;
using DevToolBox.Core.Localization;
using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tools.Password;

public sealed record PasswordOptions(
    int Length = 20,
    bool Uppercase = true,
    bool Lowercase = true,
    bool Digits = true,
    bool Symbols = true,
    bool ExcludeAmbiguous = false,
    string ExcludeCharacters = "",
    int Count = 1);

public enum PasswordStrength
{
    VeryWeak,
    Weak,
    Fair,
    Strong,
    VeryStrong,
}

public sealed record PasswordBatch(IReadOnlyList<string> Passwords, int PoolSize, double EntropyBits, PasswordStrength Strength);

/// <summary>
/// Generates passwords with a CSPRNG. <see cref="RandomNumberGenerator.GetInt32(int)"/> uses rejection
/// sampling internally, so every character is uniformly distributed (no modulo bias).
/// </summary>
public static class PasswordGenerator
{
    public const int MinLength = 4;
    public const int MaxLength = 256;
    public const int MaxCount = 1000;
    public const string UppercaseSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string LowercaseSet = "abcdefghijklmnopqrstuvwxyz";
    public const string DigitSet = "0123456789";
    public const string SymbolSet = "!@#$%^&*()-_=+[]{};:,.<>/?~|";
    public const string AmbiguousSet = "0O1lI";

    private const int MaxAttemptsPerPassword = 10_000;

    public static Result<PasswordBatch> Generate(PasswordOptions options, Func<int, int>? nextInt = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        nextInt ??= RandomNumberGenerator.GetInt32;

        if (options.Length is < MinLength or > MaxLength)
        {
            return new Error("password.length", CoreLocalization.Format(CoreStrings.Password_Length, MinLength, MaxLength));
        }

        if (options.Count is < 1 or > MaxCount)
        {
            return new Error("password.count", CoreLocalization.Format(CoreStrings.Password_Count, MaxCount));
        }

        var classes = BuildClasses(options);
        if (!classes.IsOk)
        {
            return classes.Error;
        }

        var sets = classes.Value;
        if (options.Length < sets.Count)
        {
            return new Error("password.too-short", CoreLocalization.Format(CoreStrings.Password_TooShort, sets.Count));
        }

        var pool = string.Concat(sets);
        var passwords = new string[options.Count];
        for (var i = 0; i < passwords.Length; i++)
        {
            if (GenerateOne(pool, sets, options.Length, nextInt) is not { } password)
            {
                return new Error("password.failed", CoreStrings.Password_Failed);
            }

            passwords[i] = password;
        }

        var entropy = EntropyBits(pool.Length, options.Length);
        return new PasswordBatch(passwords, pool.Length, entropy, Rate(entropy));
    }

    public static double EntropyBits(int poolSize, int length) => poolSize <= 1 ? 0 : length * Math.Log2(poolSize);

    public static PasswordStrength Rate(double entropyBits) => entropyBits switch
    {
        < 28 => PasswordStrength.VeryWeak,
        < 36 => PasswordStrength.Weak,
        < 60 => PasswordStrength.Fair,
        < 128 => PasswordStrength.Strong,
        _ => PasswordStrength.VeryStrong,
    };

    private static Result<List<string>> BuildClasses(PasswordOptions options)
    {
        var excluded = new HashSet<char>(options.ExcludeCharacters ?? string.Empty);
        if (options.ExcludeAmbiguous)
        {
            excluded.UnionWith(AmbiguousSet);
        }

        var selected = new List<(string Name, string Set)>();
        if (options.Uppercase)
        {
            selected.Add((CoreStrings.Password_Type_Uppercase, UppercaseSet));
        }

        if (options.Lowercase)
        {
            selected.Add((CoreStrings.Password_Type_Lowercase, LowercaseSet));
        }

        if (options.Digits)
        {
            selected.Add((CoreStrings.Password_Type_Digit, DigitSet));
        }

        if (options.Symbols)
        {
            selected.Add((CoreStrings.Password_Type_Symbol, SymbolSet));
        }

        if (selected.Count == 0)
        {
            return new Error("password.no-types", CoreStrings.Password_NoTypes);
        }

        var sets = new List<string>();
        foreach (var (name, set) in selected)
        {
            var remaining = string.Concat(set.Where(c => !excluded.Contains(c)));
            if (remaining.Length == 0)
            {
                return new Error("password.all-excluded", CoreLocalization.Format(CoreStrings.Password_AllExcluded, name));
            }

            sets.Add(remaining);
        }

        return sets;
    }

    /// <summary>Uniform characters from the pool; rejects the whole password if a selected type is missing.</summary>
    private static string? GenerateOne(string pool, List<string> sets, int length, Func<int, int> nextInt)
    {
        var buffer = new char[length];
        for (var attempt = 0; attempt < MaxAttemptsPerPassword; attempt++)
        {
            for (var i = 0; i < length; i++)
            {
                buffer[i] = pool[nextInt(pool.Length)];
            }

            if (sets.TrueForAll(set => buffer.AsSpan().IndexOfAny(set) >= 0))
            {
                return new string(buffer);
            }
        }

        return null;
    }
}
