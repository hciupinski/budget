using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class AuditService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger<AuditService> logger)
    : BudgetServiceBase(dbContext, httpClientFactory, marketPriceService, logger),
      IAuditService
{
    public async Task<IReadOnlyList<AuditEntryResponse>> GetAuditAsync(
        int? year,
        int? month,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0 || limit > 200)
        {
            limit = 50;
        }

        IQueryable<AuditEntry> query = dbContext.AuditEntries.AsNoTracking();

        if (year.HasValue || month.HasValue)
        {
            query = query.Where(entry =>
                (!year.HasValue || EF.Functions.ILike(entry.Payload, $"%\"year\":{year.Value}%")) &&
                (!month.HasValue || EF.Functions.ILike(entry.Payload, $"%\"month\":{month.Value}%")));
        }

        var entries = await query
            .OrderByDescending(x => x.ChangedAt)
            .Take(limit)
            .Select(x => new AuditEntryResponse(
                x.Id,
                x.ChangedAt,
                x.EntityType,
                x.EntityId,
                x.EventType,
                x.ChangedBy,
                x.Payload))
            .ToListAsync(cancellationToken);

        return entries;
    }
}
