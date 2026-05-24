namespace EcomPlatform.Application.DTOs.Integrations
{
    public class TokenData
    {
        public string AccessToken { get; init; } = string.Empty;
        public string? RefreshToken { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public string? TokenType { get; init; }
    }
}