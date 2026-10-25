namespace RestaurantManagement.Common.Results;

public record Error(string Code, string Message, object? Details = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "Giá trị truyền vào không được rỗng.");

    public string Description => Message;

    public static Error NotFound(string code, string message) => new(code, message);
    public static Error Validation(string code, string message) => new(code, message);
    public static Error Conflict(string code, string message) => new(code, message);
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Thao tác thành công không thể mang lỗi.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Thao tác thất bại phải có thông tin lỗi.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(string code, string message, object? details = null) =>
        new(false, new Error(code, message, details));
}

public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Không thể lấy giá trị từ một kết quả thất bại.");

    public static Result<T> Success(T value) => new(value, true, Error.None);
    public new static Result<T> Failure(Error error) => new(default, false, error);
    public new static Result<T> Failure(string code, string message, object? details = null) =>
        new(default, false, new Error(code, message, details));

    public static implicit operator Result<T>(T? value) =>
        value is not null ? Success(value) : Failure(Error.NullValue);
}
