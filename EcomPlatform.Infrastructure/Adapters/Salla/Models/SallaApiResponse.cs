using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Salla.Models
{
    public class SallaApiResponse<T>
    {
        [JsonPropertyName("status")]
        public int Status { get; init; }

        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("data")]
        public T? Data { get; init; }

        [JsonPropertyName("error")]
        public SallaApiError? Error { get; init; }

        [JsonPropertyName("pagination")]
        public SallaPagination? Pagination { get; init; }
    }

    public class SallaApiError
    {
        [JsonPropertyName("code")]
        public string Code { get; init; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;
    }

    public class SallaPagination
    {
        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; init; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; init; }

        [JsonPropertyName("total")]
        public int Total { get; init; }

        [JsonPropertyName("perPage")]
        public int PerPage { get; init; }
    }
}