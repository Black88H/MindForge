namespace MindForge.Models;

public class NotebookTag
{
    public Guid Id         { get; set; } = Guid.NewGuid();
    public Guid NotebookId { get; set; }
    public Guid TagId      { get; set; }
}
