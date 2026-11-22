namespace Chakra.Application.Common;

// Dipakai endpoint untuk memilih HTTP status: Failure 400, NotFound 404, Unauthorized 401, Forbidden 403
public enum ErrorType
{
    Failure,
    NotFound,
    Unauthorized,
    Forbidden
}

public class Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public ErrorType ErrorType { get; init; } = ErrorType.Failure;
    public List<ValidationError>? Errors { get; init; }

    public static Result<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
    public static Result<T> NotFound(string error) => new() { IsSuccess = false, Error = error, ErrorType = ErrorType.NotFound };
    public static Result<T> Unauthorized(string error) => new() { IsSuccess = false, Error = error, ErrorType = ErrorType.Unauthorized };
    public static Result<T> Forbidden(string error) => new() { IsSuccess = false, Error = error, ErrorType = ErrorType.Forbidden };
    public static Result<T> ValidationFailure(List<ValidationError> errors) => new()
    {
        IsSuccess = false,
        Error = "Validation failed",
        Errors = errors
    };
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ApiResponse<T>
{
    public string Status { get; set; } = string.Empty;
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data, int code = 200, string message = "Request successful") => new()
    {
        Status = "success",
        Code = code,
        Message = message,
        Data = data
    };
}

public class ApiErrorResponse
{
    public string Status { get; set; } = "error";
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ValidationError>? Errors { get; set; }

    public static ApiErrorResponse Error(string message, int code = 400) => new()
    {
        Code = code,
        Message = message
    };

    public static ApiErrorResponse Validation(string message, List<ValidationError> errors, int code = 400) => new()
    {
        Code = code,
        Message = message,
        Errors = errors
    };
}