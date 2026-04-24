using System.IO;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class SubjectRepository : IStorageService
{
    private readonly MindForgeDbContext _db;

    public SubjectRepository(MindForgeDbContext db) => _db = db;

    public async Task InitializeAsync()
    {
        await _db.Database.MigrateAsync();
    }

    public async Task<IEnumerable<Subject>> GetSubjectsAsync()
        => await _db.Subjects.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync();

    public async Task SaveSubjectAsync(Subject subject)
    {
        var existing = await _db.Subjects.FindAsync(subject.Id);
        if (existing == null)
            _db.Subjects.Add(subject);
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(subject);
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSubjectAsync(Guid id)
    {
        var subject = await _db.Subjects.FindAsync(id);
        if (subject != null)
        {
            _db.Subjects.Remove(subject);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        // Einstellungen aus JSON-Datei laden (kein DB-Record nötig)
        await Task.CompletedTask;
        return Utils.Configuration.Load();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await Task.CompletedTask;
        Utils.Configuration.Save(settings);
    }

    public async Task<UserProgress> GetUserProgressAsync()
    {
        var progress = await _db.UserProgress
            .Where(p => p.UserId == "default" && p.SubjectId == null)
            .FirstOrDefaultAsync();

        return progress ?? new UserProgress { UserId = "default" };
    }

    public async Task SaveUserProgressAsync(UserProgress progress)
    {
        var existing = await _db.UserProgress.FindAsync(progress.Id);
        if (existing == null)
            _db.UserProgress.Add(progress);
        else
            _db.Entry(existing).CurrentValues.SetValues(progress);
        await _db.SaveChangesAsync();
    }

    public async Task BackupAsync(string path)
    {
        var dbPath = MindForgeDbContext.GetDbPath();
        if (File.Exists(dbPath))
            File.Copy(dbPath, path, overwrite: true);
        await Task.CompletedTask;
    }

    public async Task RestoreAsync(string path)
    {
        var dbPath = MindForgeDbContext.GetDbPath();
        if (File.Exists(path))
        {
            File.Copy(path, dbPath, overwrite: true);
            await _db.Database.MigrateAsync();
        }
    }
}
