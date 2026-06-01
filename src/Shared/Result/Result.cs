namespace GymAffiliate.Shared.Result;

public sealed class Result<T>
{
    public bool IsSuccess { get; private init; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; private init; }
    public ResultError? Error { get; private init; }
    private Result() { }
    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(ResultError error) => new() { IsSuccess = false, Error = error };
    public static Result<T> Failure(string code, string message, int httpStatus = 400) =>
        Failure(new ResultError(code, message, httpStatus));
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess ? Result<TOut>.Success(mapper(Value!)) : Result<TOut>.Failure(Error!);
}

public sealed class Result
{
    public bool IsSuccess { get; private init; }
    public bool IsFailure => !IsSuccess;
    public ResultError? Error { get; private init; }
    private Result() { }
    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(ResultError error) => new() { IsSuccess = false, Error = error };
    public static Result Failure(string code, string message, int httpStatus = 400) =>
        Failure(new ResultError(code, message, httpStatus));
}

public record ResultError(
    string Code, string Message, int HttpStatus = 400,
    Dictionary<string, string[]>? Details = null);

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(ResultError error) => new()
    {
        Success = false,
        Error = new ApiError(error.Code, error.Message, error.HttpStatus,
            DateTimeOffset.UtcNow, null, error.Details)
    };
}

public sealed class ApiResponse
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public ApiError? Error { get; init; }
    public static ApiResponse Ok(string message = "Operacion exitosa") =>
        new() { Success = true, Data = new { message } };
    public static ApiResponse Fail(ResultError error) => new()
    {
        Success = false,
        Error = new ApiError(error.Code, error.Message, error.HttpStatus, DateTimeOffset.UtcNow)
    };
}

public record ApiError(string Code, string Message, int Status, DateTimeOffset Timestamp,
    string? TraceId = null, Dictionary<string, string[]>? Details = null);

public sealed class PagedApiResponse<T>
{
    public bool Success { get; init; } = true;
    public PagedData<T>? Data { get; init; }
    public ApiError? Error { get; init; }
    public static PagedApiResponse<T> Ok(IEnumerable<T> items, int totalCount, int page, int pageSize) => new()
    {
        Data = new PagedData<T>
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        }
    };
    public static PagedApiResponse<T> Fail(ResultError error) => new()
    {
        Success = false,
        Error = new ApiError(error.Code, error.Message, error.HttpStatus, DateTimeOffset.UtcNow)
    };
}

public sealed class PagedData<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
