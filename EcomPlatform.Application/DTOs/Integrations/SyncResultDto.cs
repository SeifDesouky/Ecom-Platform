using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Integrations
{
    public class SyncResultDto
    {
        public Guid SyncLogId { get; init; }
        public SyncEntityType EntityType { get; init; }
        public SyncDirection Direction { get; init; }
        public SyncStatus Status { get; init; }
        public int TotalRecords { get; init; }
        public int SuccessCount { get; init; }
        public int FailedCount { get; init; }
        public double? DurationSeconds { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}