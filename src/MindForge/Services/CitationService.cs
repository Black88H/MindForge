using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class CitationService : ICitationService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector         _ai;

    public CitationService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<Citation> CreateFromMaterialAsync(Guid materialId, Guid userId)
    {
        var material = await _db.Materials.FindAsync(materialId);
        if (material is null)
            throw new ArgumentException("Material not found", nameof(materialId));

        // Check if citation already exists
        var existing = await _db.Citations
            .FirstOrDefaultAsync(c => c.MaterialId == materialId && c.UserId == userId);
        if (existing is not null) return existing;

        // Try to extract metadata via AI
        var contentSnippet = material.KiContent?.Length > 0
            ? material.KiContent[..Math.Min(material.KiContent.Length, 1500)]
            : "";

        var fileName = material.OriginalFileName ?? "Unknown";

        string title = fileName;
        string authors = "Unbekannt";
        string year = DateTime.UtcNow.Year.ToString();
        string publisher = "";
        string doi = "";
        string url = "";

        if (!string.IsNullOrWhiteSpace(contentSnippet))
        {
            var prompt =
                "Extract bibliographic metadata from this academic text. Respond ONLY with JSON:\n" +
                "{ \"title\": \"...\", \"authors\": \"...\", \"year\": \"2024\", \"publisher\": \"...\", \"doi\": \"\", \"url\": \"\" }\n\n" +
                "TEXT:\n" + contentSnippet;

            try
            {
                var (provider, model) = await _ai.SelectAsync(AITask.Summarization);
                var json = await provider.GenerateAsync(model, prompt);

                var start = json.IndexOf('{');
                var end   = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    using var doc  = JsonDocument.Parse(json[start..(end + 1)]);
                    var root       = doc.RootElement;
                    if (root.TryGetProperty("title",     out var t))   title     = t.GetString() ?? fileName;
                    if (root.TryGetProperty("authors",   out var a))   authors   = a.GetString() ?? "Unbekannt";
                    if (root.TryGetProperty("year",      out var y))   year      = y.GetString() ?? year;
                    if (root.TryGetProperty("publisher", out var pub)) publisher = pub.GetString() ?? "";
                    if (root.TryGetProperty("doi",       out var d))   doi       = d.GetString() ?? "";
                    if (root.TryGetProperty("url",       out var u))   url       = u.GetString() ?? "";
                }
            }
            catch { /* use defaults */ }
        }

        var key     = BuildCitationKey(authors, year);
        var apa     = BuildAPA(authors, year, title, publisher, doi);
        var mla     = BuildMLA(authors, title, publisher, year);
        var chicago = BuildChicago(authors, title, publisher, year, doi);
        var bibtex  = BuildBibtex(key, title, authors, year, publisher, doi, url);

        var citation = new Citation
        {
            Id           = Guid.NewGuid(),
            MaterialId   = materialId,
            UserId       = userId,
            CitationKey  = key,
            Title        = title,
            Authors      = authors,
            Year         = year,
            Publisher    = publisher,
            DOI          = doi,
            URL          = url,
            BibtexEntry  = bibtex,
            APAFormat    = apa,
            MLAFormat    = mla,
            ChicagoFormat = chicago,
            CreatedAt    = DateTime.UtcNow
        };

        _db.Citations.Add(citation);
        await _db.SaveChangesAsync();
        return citation;
    }

    public async Task<List<Citation>> GetForNotebookAsync(Guid notebookId)
    {
        // Get all materialIds for this notebook
        var materialIds = await _db.Materials
            .Where(m => m.NotebookId == notebookId)
            .Select(m => m.Id)
            .ToListAsync();

        return await _db.Citations
            .Where(c => materialIds.Contains(c.MaterialId))
            .OrderBy(c => c.Authors)
            .ToListAsync();
    }

    public async Task<string> GenerateBibliographyAsync(Guid notebookId, string format = "APA")
    {
        var citations = await GetForNotebookAsync(notebookId);
        if (!citations.Any())
            return "Keine Quellen für dieses Notizbuch gefunden.";

        var sb = new StringBuilder();

        if (format.Equals("BibTeX", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var c in citations)
                sb.AppendLine(c.BibtexEntry).AppendLine();
            return sb.ToString().Trim();
        }

        sb.AppendLine(format.ToUpper() + " BIBLIOGRAPHY");
        sb.AppendLine(new string('─', 50));

        foreach (var c in citations.OrderBy(c => c.Authors))
        {
            var entry = format.ToUpper() switch
            {
                "MLA"     => c.MLAFormat,
                "CHICAGO" => c.ChicagoFormat,
                _         => c.APAFormat
            };
            sb.AppendLine(entry);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    // ── Citation format builders ───────────────────────────────────────────────

    private static string BuildCitationKey(string authors, string year)
    {
        var firstAuthor = authors.Split(',')[0].Split(' ').Last();
        var clean = new string(firstAuthor.Where(char.IsLetterOrDigit).ToArray());
        return (clean + year)[..Math.Min((clean + year).Length, 12)];
    }

    private static string BuildAPA(string authors, string year, string title, string publisher, string doi)
    {
        var sb = new StringBuilder();
        sb.Append(authors);
        if (!authors.EndsWith('.')) sb.Append('.');
        sb.Append(" (").Append(year).Append("). ");
        sb.Append(title);
        if (!title.EndsWith('.')) sb.Append('.');
        if (!string.IsNullOrWhiteSpace(publisher))
            sb.Append(" ").Append(publisher).Append('.');
        if (!string.IsNullOrWhiteSpace(doi))
            sb.Append(" https://doi.org/").Append(doi);
        return sb.ToString();
    }

    private static string BuildMLA(string authors, string title, string publisher, string year)
    {
        var sb = new StringBuilder();
        sb.Append(authors);
        if (!authors.EndsWith('.')) sb.Append('.');
        sb.Append(" \"").Append(title).Append(".\" ");
        if (!string.IsNullOrWhiteSpace(publisher))
            sb.Append(publisher).Append(", ");
        sb.Append(year).Append('.');
        return sb.ToString();
    }

    private static string BuildChicago(string authors, string title, string publisher, string year, string doi)
    {
        var sb = new StringBuilder();
        sb.Append(authors);
        if (!authors.EndsWith('.')) sb.Append('.');
        sb.Append(" \"").Append(title).Append('.').Append('"');
        if (!string.IsNullOrWhiteSpace(publisher))
            sb.Append(' ').Append(publisher);
        sb.Append(" (").Append(year).Append(").");
        if (!string.IsNullOrWhiteSpace(doi))
            sb.Append(" https://doi.org/").Append(doi);
        return sb.ToString();
    }

    private static string BuildBibtex(string key, string title, string authors, string year,
        string publisher, string doi, string url)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@article{" + key + ",");
        sb.AppendLine("  title     = {" + title + "},");
        sb.AppendLine("  author    = {" + authors + "},");
        sb.AppendLine("  year      = {" + year + "},");
        if (!string.IsNullOrWhiteSpace(publisher))
            sb.AppendLine("  publisher = {" + publisher + "},");
        if (!string.IsNullOrWhiteSpace(doi))
            sb.AppendLine("  doi       = {" + doi + "},");
        if (!string.IsNullOrWhiteSpace(url))
            sb.AppendLine("  url       = {" + url + "},");
        sb.Append("}");
        return sb.ToString();
    }
}
