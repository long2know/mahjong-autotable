namespace Mahjong.Autotable.Api.Data.Entities;

public class TableSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleSet { get; set; } = "changsha";
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
