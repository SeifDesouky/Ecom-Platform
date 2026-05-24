using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Integrations
{
    public class SyncRequestDto
    {
        public SyncEntityType EntityType { get; init; }
        public SyncDirection Direction { get; init; }
    }
}