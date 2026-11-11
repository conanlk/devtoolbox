using DevToolBox.App.Tools.Password;
using DevToolBox.App.Tools.Uuid;
using DevToolBox.Core.Tools.Password;
using DevToolBox.Core.Tools.Uuid;

namespace DevToolBox.App.Tests.Tools;

public class UuidViewModelTests
{
    [Fact]
    public void StartsWithFiveV4Uuids()
    {
        var vm = new UuidViewModel();

        var lines = vm.Output.Split(Environment.NewLine);
        Assert.Equal(5, lines.Length);
        Assert.All(lines, l => Assert.Equal(4, UuidTools.Inspect(l).Value.Version));
    }

    [Fact]
    public void FormatOptions_ReformatTheSameUuids()
    {
        var vm = new UuidViewModel();
        var before = vm.Output;

        vm.Uppercase = true;
        vm.Hyphens = false;
        vm.Braces = true;

        Assert.Equal(before.Split(Environment.NewLine).Select(l => "{" + l.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant() + "}"),
            vm.Output.Split(Environment.NewLine));
    }

    [Fact]
    public void VersionAndCount_Regenerate()
    {
        var vm = new UuidViewModel { Count = 3 };

        vm.SelectedVersion = vm.Versions.Single(v => v.Value == UuidVersion.V7);

        var lines = vm.Output.Split(Environment.NewLine);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, l => Assert.Equal(7, UuidTools.Inspect(l).Value.Version));
    }

    [Fact]
    public void Inspector_ShowsDetails_OrError()
    {
        var vm = new UuidViewModel();
        vm.SelectedVersion = vm.Versions[1];

        vm.InspectFirstCommand.Execute(null);
        Assert.Contains(vm.InspectFields, f => f.Label == "Version" && f.Value.StartsWith("Version 7", StringComparison.Ordinal));
        Assert.Contains(vm.InspectFields, f => f.Label == "Timestamp (UTC)");

        vm.InspectInput = "nope";
        Assert.NotNull(vm.InspectError);
        Assert.Empty(vm.InspectFields);
    }
}

public class PasswordViewModelTests
{
    [Fact]
    public void Generates_OnCreation_WithStrength()
    {
        var vm = new PasswordViewModel();

        var password = Assert.Single(vm.Passwords);
        Assert.Equal(20, password.Length);
        Assert.True(vm.IsStrong);
        Assert.Contains("bits of entropy", vm.StrengthText, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionChanges_Regenerate()
    {
        var vm = new PasswordViewModel();

        vm.Count = 10;
        vm.Length = 8;
        vm.Symbols = false;
        vm.Uppercase = false;

        Assert.Equal(10, vm.Passwords.Count);
        Assert.All(vm.Passwords, p => Assert.Matches("^[a-z0-9]{8}$", p));
        Assert.Equal(string.Join(Environment.NewLine, vm.Passwords), vm.AllPasswords);
    }

    [Fact]
    public void ShortDigitsOnly_IsWeak()
    {
        var vm = new PasswordViewModel { Uppercase = false, Lowercase = false, Symbols = false };

        vm.Length = 10; // 10 × log2(10) ≈ 33 bits

        Assert.Equal(PasswordStrength.Weak, vm.Strength);
        Assert.True(vm.IsWeak);
    }

    [Fact]
    public void NoCharacterTypes_ShowsError()
    {
        var vm = new PasswordViewModel { Uppercase = false, Lowercase = false, Digits = false };

        vm.Symbols = false;

        Assert.Empty(vm.Passwords);
        Assert.NotNull(vm.ErrorMessage);
    }
}
