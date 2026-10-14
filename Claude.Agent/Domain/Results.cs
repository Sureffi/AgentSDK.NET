using System.Diagnostics.CodeAnalysis;

namespace Claude.Agent;

internal readonly record struct Unit
{
    public static readonly Unit Value;
}

internal readonly struct Result<T, TError>
    where TError : notnull
{
    private readonly T _value;
    private readonly TError _error;

    private Result(T value, TError error, bool isOk)
    {
        _value = value;
        _error = error;
        IsOk = isOk;
    }

    public bool IsOk { get; }

    public bool IsError => !IsOk;

    public T Value => IsOk ? _value : throw new InvalidOperationException(ErrorText());

    public TError Error => IsOk ? throw new InvalidOperationException("The result is ok and carries no error.") : _error;

    public static Result<T, TError> Ok(T value) => new(value, default!, true);

    public static Result<T, TError> Fail(TError error) => new(default!, error, false);

    public TOut Match<TOut>(Func<T, TOut> ok, Func<TError, TOut> fail)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(fail);
        return IsOk ? ok(_value) : fail(_error);
    }

    public Result<TOut, TError> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsOk ? Result<TOut, TError>.Ok(map(_value)) : Result<TOut, TError>.Fail(_error);
    }

    public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return IsOk ? bind(_value) : Result<TOut, TError>.Fail(_error);
    }

    public bool TryGet([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out TError error)
    {
        value = _value;
        error = _error;
        return IsOk;
    }

    public override string ToString() => IsOk ? $"Ok({_value})" : $"Fail({_error})";

    public static implicit operator Result<T, TError>(T value) => Ok(value);

    public static implicit operator Result<T, TError>(TError error) => Fail(error);

    private string ErrorText() =>
        _error is null ? "The result is a failure." : _error.ToString() ?? "The result is a failure.";
}

internal static class Result
{
    public static Result<T, TError> Ok<T, TError>(T value)
        where TError : notnull => Result<T, TError>.Ok(value);

    public static Result<T, TError> Fail<T, TError>(TError error)
        where TError : notnull => Result<T, TError>.Fail(error);
}
