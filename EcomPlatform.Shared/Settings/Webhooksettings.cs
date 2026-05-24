namespace EcomPlatform.Shared.Settings
{
    public sealed class WebhookSettings
    {
        /// <summary>الـ secret اللي Salla بتوقع بيه الـ webhook</summary>
        public string SallaSecret { get; init; } = string.Empty;

        /// <summary>الـ secret اللي Zid بتوقع بيه الـ webhook</summary>
        public string ZidSecret { get; init; } = string.Empty;
    }
}