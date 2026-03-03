namespace Budget.Api.Modules.Budget.Domain;

public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
