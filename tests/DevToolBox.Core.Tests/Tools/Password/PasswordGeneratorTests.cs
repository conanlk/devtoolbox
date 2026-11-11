using DevToolBox.Core.Tools.Password;

namespace DevToolBox.Core.Tests.Tools.Password;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generate_RespectsLengthCountAndIncludesEverySelectedType()
    {
        var batch = PasswordGenerator.Generate(new PasswordOptions(Length: 12, Count: 200)).Value;

        Assert.Equal(200, batch.Passwords.Count);
        Assert.All(batch.Passwords, p =>
        {
            Assert.Equal(12, p.Length);
            Assert.Contains(p, char.IsAsciiLetterUpper);
            Assert.Contains(p, char.IsAsciiLetterLower);
            Assert.Contains(p, char.IsAsciiDigit);
            Assert.Contains(p, c => PasswordGenerator.SymbolSet.Contains(c, StringComparison.Ordinal));
        });
        Assert.Equal(26 + 26 + 10 + PasswordGenerator.SymbolSet.Length, batch.PoolSize);
    }

    [Fact]
    public void Generate_ExcludesAmbiguousAndCustomCharacters()
    {
        var batch = PasswordGenerator.Generate(new PasswordOptions(Length: 64, ExcludeAmbiguous: true, ExcludeCharacters: "abc#", Count: 50)).Value;

        Assert.All(batch.Passwords, p => Assert.DoesNotContain(p, c => "0O1lIabc#".Contains(c, StringComparison.Ordinal)));
    }

    [Fact]
    public void Generate_OnlyDigits()
    {
        var batch = PasswordGenerator.Generate(new PasswordOptions(Length: 8, Uppercase: false, Lowercase: false, Symbols: false)).Value;

        Assert.All(batch.Passwords[0], c => Assert.True(char.IsAsciiDigit(c)));
        Assert.Equal(10, batch.PoolSize);
    }

    [Theory]
    [InlineData(3, 1, "password.length")]
    [InlineData(257, 1, "password.length")]
    [InlineData(10, 0, "password.count")]
    [InlineData(10, 1001, "password.count")]
    public void Generate_ValidatesRanges(int length, int count, string code) =>
        Assert.Equal(code, PasswordGenerator.Generate(new PasswordOptions(Length: length, Count: count)).Error?.Code);

    [Fact]
    public void Generate_NoTypes_IsAnError() =>
        Assert.Equal("password.no-types", PasswordGenerator.Generate(new PasswordOptions(Uppercase: false, Lowercase: false, Digits: false, Symbols: false)).Error?.Code);

    [Fact]
    public void Generate_AllOfATypeExcluded_IsAnError() =>
        Assert.Equal("password.all-excluded", PasswordGenerator.Generate(new PasswordOptions(ExcludeCharacters: "0123456789")).Error?.Code);

    [Fact]
    public void Generate_IsUniform_ChiSquare()
    {
        // 10 digits × 100k samples: chi-square with 9 dof should stay well below 30 (p ≈ 0.0004).
        var batch = PasswordGenerator.Generate(new PasswordOptions(Length: 100, Uppercase: false, Lowercase: false, Symbols: false, Count: 1000)).Value;
        var counts = new int[10];
        foreach (var c in batch.Passwords.SelectMany(p => p))
        {
            counts[c - '0']++;
        }

        var expected = 100_000 / 10.0;
        var chiSquare = counts.Sum(o => (o - expected) * (o - expected) / expected);
        Assert.True(chiSquare < 30, $"chi-square {chiSquare:F1} suggests bias");
    }

    [Fact]
    public void Generate_UsesInjectedRandomSource()
    {
        var batch = PasswordGenerator.Generate(new PasswordOptions(Length: 4, Uppercase: false, Lowercase: false, Symbols: false), _ => 7).Value;

        Assert.Equal("7777", batch.Passwords[0]);
    }

    [Theory]
    [InlineData(20, PasswordStrength.VeryWeak)]
    [InlineData(30, PasswordStrength.Weak)]
    [InlineData(50, PasswordStrength.Fair)]
    [InlineData(100, PasswordStrength.Strong)]
    [InlineData(130, PasswordStrength.VeryStrong)]
    public void Rate_MapsEntropyToStrength(double bits, PasswordStrength expected) =>
        Assert.Equal(expected, PasswordGenerator.Rate(bits));

    [Fact]
    public void EntropyBits_IsLengthTimesLog2Pool() =>
        Assert.Equal(16 * Math.Log2(62), PasswordGenerator.EntropyBits(62, 16), precision: 6);
}
