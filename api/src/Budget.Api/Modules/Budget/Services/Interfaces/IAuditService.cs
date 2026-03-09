using Budget.Api.Modules.Budget.Contracts;

namespace Budget.Api.Modules.Budget.Services;

public interface IAuditService
{
    Task<IReadOnlyList<AuditEntryResponse>> GetAuditAsync(
        int? year,
        int? month,
        int limit,
        CancellationToken cancellationToken);
}
