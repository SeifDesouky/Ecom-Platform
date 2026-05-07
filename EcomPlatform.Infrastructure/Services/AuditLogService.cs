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

        public async Task LogAsync(string entityName, string entityId, AuditAction action,
            Guid userId, Guid? tenantId, string oldValue = "", string newValue = "",
            string ipAddress = "", string userAgent = "")
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
                UserId = userId,
                TenantId = tenantId
            };

            await _unitOfWork.AuditLogs.AddAsync(log);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByEntityAsync(
            string entityName, string entityId)
        {
            var logs = await _unitOfWork.AuditLogs.FindAsync(l =>
                l.EntityName == entityName && l.EntityId == entityId);

            var result = logs.OrderByDescending(l => l.CreatedAt);
            return ApiResponse<IEnumerable<AuditLogResponseDto>>.Ok(
                await EnrichWithUserNames(result));
        }

        public async Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByTenantAsync(
            Guid tenantId, int page = 1, int pageSize = 50)
        {
            var logs = await _unitOfWork.AuditLogs.FindAsync(l => l.TenantId == tenantId);

            var result = logs.OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return ApiResponse<IEnumerable<AuditLogResponseDto>>.Ok(
                await EnrichWithUserNames(result));
        }

        public async Task<ApiResponse<IEnumerable<AuditLogResponseDto>>> GetByUserAsync(
            Guid userId, int page = 1, int pageSize = 50)
        {
            var logs = await _unitOfWork.AuditLogs.FindAsync(l => l.UserId == userId);

            var result = logs.OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return ApiResponse<IEnumerable<AuditLogResponseDto>>.Ok(
                await EnrichWithUserNames(result));
        }

        private async Task<IEnumerable<AuditLogResponseDto>> EnrichWithUserNames(
            IEnumerable<AuditLog> logs)
        {
            var result = new List<AuditLogResponseDto>();
            foreach (var log in logs)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(log.UserId);
                result.Add(new AuditLogResponseDto
                {
                    Id = log.Id,
                    EntityName = log.EntityName,
                    EntityId = log.EntityId,
                    Action = log.Action,
                    OldValue = log.OldValue,
                    NewValue = log.NewValue,
                    IPAddress = log.IPAddress,
                    UserId = log.UserId,
                    UserName = user != null ? $"{user.FirstName} {user.LastName}" : string.Empty,
                    TenantId = log.TenantId,
                    CreatedAt = log.CreatedAt
                });
            }
            return result;
        }
    }
}