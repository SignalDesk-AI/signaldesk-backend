namespace BuildingBlocks.Application.Results;

public class Result
{
    protected Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public static Result Success()
    {
        return new Result(true, null, null);
    }

    public static Result Failure(string errorCode, string errorMessage)
    {
        return new Result(false, errorCode, errorMessage);
    }
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
        : base(isSuccess, errorCode, errorMessage)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value, null, null);
    }

    public static new Result<T> Failure(string errorCode, string errorMessage)
    {
        return new Result<T>(false, default, errorCode, errorMessage);
    }
}
