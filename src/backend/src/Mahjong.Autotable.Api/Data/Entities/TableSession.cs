namespace Mahjong.Autotable.Api.Data.Entities;

public class TableSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleSet { get; set; } = "changsha";
    public string StateJson { get; set; } = "{}";
    public int StateVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastActionUtc { get; set; }
}
