namespace VanAnhPerfume.Models.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? ChangedBy { get; set; }
    public string? Payload { get; set; }
    public DateTime CreatedAt { get; set; }
}
