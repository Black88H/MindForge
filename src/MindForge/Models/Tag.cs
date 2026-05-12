namespace MindForge.Models;

public class Tag
{
    public Guid   Id     { get; set; } = Guid.NewGuid();
    public Guid   UserId { get; set; }
    public string Name   { get; set; } = string.Empty;
    public string Color  { get; set; } = "#6366F1";
}
