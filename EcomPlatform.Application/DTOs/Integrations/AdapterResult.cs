namespace EcomPlatform.Application.DTOs.Integrations
{
    public class AdapterResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
        public int? HttpStatusCode { get; init; }

        public static AdapterResult Success() => new() { IsSuccess = true };

        public static AdapterResult Failure(string error, string? code = null, int? statusCode = null) =>
            new() { IsSuccess = false, ErrorMessage = error, ErrorCode = code, HttpStatusCode = statusCode };
    }

    public class AdapterResult<T> : AdapterResult
    {
        public T? Data { get; init; }

        public static AdapterResult<T> Success(T data) =>
            new() { IsSuccess = true, Data = data };

        public static new AdapterResult<T> Failure(string error, string? code = null, int? statusCode = null) =>
            new() { IsSuccess = false, ErrorMessage = error, ErrorCode = code, HttpStatusCode = statusCode };
    }
}