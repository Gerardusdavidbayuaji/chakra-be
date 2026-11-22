using Chakra.Application.Common;

namespace Chakra.API.Endpoints;

public static class ResultHttpExtensions
{
    // Mengubah Result yang gagal menjadi response HTTP sesuai ErrorType
    public static IResult ToErrorResult<T>(this Result<T> result) => result.ErrorType switch
    {
        ErrorType.Unauthorized => Results.Json(ApiErrorResponse.Error(result.Error!, 401), statusCode: 401),
        ErrorType.Forbidden => Results.Json(ApiErrorResponse.Error(result.Error!, 403), statusCode: 403),
        ErrorType.NotFound => Results.NotFound(ApiErrorResponse.Error(result.Error!, 404)),
        _ => result.Errors != null
            ? Results.BadRequest(ApiErrorResponse.Validation(result.Error!, result.Errors, 400))
            : Results.BadRequest(ApiErrorResponse.Error(result.Error!, 400))
    };
}
