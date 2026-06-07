using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.AuditLogs;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AuditLogService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task LogAsync(
            string entityName,
            string entityId,
            AuditAction action,
            Guid? userId,
            Guid? tenantId,
            string oldValue = "",
            string newValue = "",
            string ipAddress = "",
            string userAgent = "")
        {
            var log = new AuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                UserId = (userId.HasValue && userId.Value != Guid.Empty) ? userId : null,
                TenantId = tenantId
            };

            await _unitOfWork.AuditLogs.AddAsync(log);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByEntityAsync(
            string entityName,
            string entityId)
        {
            var logs = await _unitOfWork.AuditLogs.FindAsync(l =>
                l.EntityName == entityName && l.EntityId == entityId);

            var ordered = logs.OrderByDescending(l => l.CreatedAt).ToList();

            return ApiResponse<IEnumerable<AuditLogResponseDto>>.Ok(
                await EnrichWithUserNamesAsync(ordered));
        }

        public async Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByTenantAsync(
            Guid tenantId,
            int page = 1,
            int pageSize = 50)
        {
            var logs = await _unitOfWork.AuditLogs.FindAsync(l => l.TenantId == tenantId);

            var paged = logs
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return ApiResponse<IEnumerable<AuditLogResponseDto>>.Ok(
                await EnrichWithUserNamesAsync(paged));
        }

        public async Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByUserAsync(
            Guid userId,
            int page = 1,
            int pageSize = 50)
        {
            var logs = await _unitOfWork.AuditLogs.FindAsync(l => l.UserId == userId);

            var paged = logs
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return ApiResponse<IEnumerable<AuditLogResponseDto>>.Ok(
                await EnrichWithUserNamesAsync(paged));
        }

        // ── Private Helpers ───────────────────────────────────────────────

        private async Task<IEnumerable<AuditLogResponseDto>> EnrichWithUserNamesAsync(
            IReadOnlyList<AuditLog> logs)
        {
            if (logs.Count == 0)
                return Enumerable.Empty<AuditLogResponseDto>();

            var userIds = logs
                .Where(l => l.UserId.HasValue && l.UserId.Value != Guid.Empty)
                .Select(l => l.UserId!.Value)
                .Distinct()
                .ToList();

            var usersDict = userIds.Count > 0
                ? (await _unitOfWork.Users.FindAsync(u => userIds.Contains(u.Id)))
                    .ToDictionary(u => u.Id)
                : new Dictionary<Guid, User>();

            return logs.Select(log =>
            {
                User? user = null;
                if (log.UserId.HasValue && log.UserId.Value != Guid.Empty)
                    usersDict.TryGetValue(log.UserId.Value, out user);

                return new AuditLogResponseDto
                {
                    Id = log.Id,
                    EntityName = log.EntityName,
                    EntityId = log.EntityId,
                    Action = log.Action,
                    OldValue = log.OldValue,
                    NewValue = log.NewValue,
                    IPAddress = log.IPAddress,
                    UserId = log.UserId,
                    UserName = user is not null
                        ? $"{user.FirstName} {user.LastName}".Trim()
                        : string.Empty,
                    TenantId = log.TenantId,
                    CreatedAt = log.CreatedAt
                };
            });
        }
    }
}