using DevToolBox.Core.Shared;

namespace DevToolBox.Core.Tests.Shared;

public class ResultTests
{
    [Fact]
    public void Ok_ExposesValue()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsOk);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_ExposesError()
    {
        var result = Result<int>.Fail("x.bad", "Bad input");

        Assert.False(result.IsOk);
        Assert.Equal(new Error("x.bad", "Bad input"), result.Error);
    }

    [Fact]
    public void Value_OnFailure_Throws()
    {
        var result = Result<int>.Fail("x.bad", "Bad input");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Fail_WithNullError_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Result<int>.Fail(null!));

    [Fact]
    public void ImplicitConversions_CreateOkAndFail()
    {
        Result<string> ok = "value";
        Result<string> fail = new Error("x.bad", "Bad");

        Assert.True(ok.IsOk);
        Assert.False(fail.IsOk);
    }

    [Fact]
    public void Map_TransformsValue_AndPropagatesError()
    {
        var mapped = Result<int>.Ok(2).Map(v => v * 10);
        var failed = Result<int>.Fail("x.bad", "Bad").Map(v => v * 10);

        Assert.Equal(20, mapped.Value);
        Assert.Equal("x.bad", failed.Error?.Code);
    }

    [Fact]
    public void Match_CallsMatchingBranch()
    {
        Assert.Equal("ok:1", Result<int>.Ok(1).Match(v => $"ok:{v}", e => $"err:{e.Code}"));
        Assert.Equal("err:x", Result<int>.Fail("x", "m").Match(v => $"ok:{v}", e => $"err:{e.Code}"));
    }
}
