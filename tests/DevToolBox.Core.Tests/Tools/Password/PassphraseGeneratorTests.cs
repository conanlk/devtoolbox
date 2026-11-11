using DevToolBox.Core.Tools.Password;

namespace DevToolBox.Core.Tests.Tools.Password;

public class PassphraseGeneratorTests
{
    [Fact]
    public void Wordlist_IsTheEffLargeList()
    {
        Assert.Equal(7776, PassphraseGenerator.Words.Count);
        Assert.Equal(7776, PassphraseGenerator.Words.Distinct().Count());
        Assert.Equal("abacus", PassphraseGenerator.Words[0]);
        Assert.Equal("zoom", PassphraseGenerator.Words[^1]);
    }

    [Fact]
    public void Generate_UsesWordsSeparatorAndCount()
    {
        var batch = PassphraseGenerator.Generate(new PassphraseOptions(Words: 6, Separator: ".", Count: 20)).Value;

        Assert.Equal(20, batch.Passwords.Count);
        Assert.All(batch.Passwords, p =>
        {
            var words = p.Split('.');
            Assert.Equal(6, words.Length);
            Assert.All(words, w => Assert.Contains(w, PassphraseGenerator.Words));
        });
        Assert.Equal(7776, batch.PoolSize);
    }

    [Fact]
    public void Generate_CapitalizeAndNumber()
    {
        var phrase = PassphraseGenerator.Generate(new PassphraseOptions(Words: 4, Separator: " ", Capitalize: true, IncludeNumber: true)).Value.Passwords[0];

        var words = phrase.Split(' ');
        Assert.All(words, w => Assert.True(char.IsUpper(w[0])));
        Assert.Single(words, w => char.IsAsciiDigit(w[^1]));
    }

    [Fact]
    public void Generate_IsDeterministicWithInjectedRandom() =>
        Assert.Equal("abacus abacus abacus", PassphraseGenerator.Generate(new PassphraseOptions(Words: 3), _ => 0).Value.Passwords[0]);

    [Fact]
    public void Entropy_IsAbout12Point9BitsPerWord()
    {
        Assert.Equal(5 * Math.Log2(7776), PassphraseGenerator.EntropyBits(5, includeNumber: false), precision: 6);
        Assert.Equal((5 * Math.Log2(7776)) + Math.Log2(50), PassphraseGenerator.EntropyBits(5, includeNumber: true), precision: 6);
        Assert.Equal(PasswordStrength.Fair, PassphraseGenerator.Generate(new PassphraseOptions(Words: 4)).Value.Strength);
        Assert.Equal(PasswordStrength.Strong, PassphraseGenerator.Generate(new PassphraseOptions(Words: 6)).Value.Strength);
    }

    [Theory]
    [InlineData(2, "-", 1, "passphrase.words")]
    [InlineData(13, "-", 1, "passphrase.words")]
    [InlineData(5, "----", 1, "passphrase.separator")]
    [InlineData(5, "-", 0, "password.count")]
    public void Generate_ValidatesOptions(int words, string separator, int count, string code) =>
        Assert.Equal(code, PassphraseGenerator.Generate(new PassphraseOptions(words, separator, Count: count)).Error?.Code);
}
