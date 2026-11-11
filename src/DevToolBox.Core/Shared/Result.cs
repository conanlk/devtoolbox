using System.Diagnostics.CodeAnalysis;

namespace DevToolBox.Core.Shared;

/// <summary>
/// Outcome of a tool operation: either a value or an <see cref="Error"/>, never both.
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T? value, Error? error, bool isOk)
    {
        _value = value;
        _error = error;
        IsOk = isOk;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsOk { get; }

    public T Value => IsOk
        ? _value!
        : throw new InvalidOperationException($"Result is an error: {_error!.Code}");

    public Error? Error => _error;

    public static Result<T> Ok(T value) => new(value, null, isOk: true);

    public static Result<T> Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default, error, isOk: false);
    }

    public static Result<T> Fail(string code, string message) => Fail(new Error(code, message));

    public TOut Match<TOut>(Func<T, TOut> onOk, Func<Error, TOut> onError)
    {
        ArgumentNullException.ThrowIfNull(onOk);
        ArgumentNullException.ThrowIfNull(onError);
        return IsOk ? onOk(_value!) : onError(Error);
    }

    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsOk ? Result<TOut>.Ok(map(_value!)) : Result<TOut>.Fail(Error);
    }

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(Error error) => Fail(error);
}
