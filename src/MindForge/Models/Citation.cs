namespace MindForge.Models;

public class Citation
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public Guid   MaterialId   { get; set; }
    public Guid   UserId       { get; set; }
    public string CitationKey  { get; set; } = string.Empty;
    public string Title        { get; set; } = string.Empty;
    public string Authors      { get; set; } = string.Empty;   // semicolon-separated
    public string Year         { get; set; } = string.Empty;
    public string Publisher    { get; set; } = string.Empty;
    public string DOI          { get; set; } = string.Empty;
    public string URL          { get; set; } = string.Empty;
    public string BibtexEntry  { get; set; } = string.Empty;
    public string APAFormat    { get; set; } = string.Empty;
    public string MLAFormat    { get; set; } = string.Empty;
    public string ChicagoFormat { get; set; } = string.Empty;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
}
