using MindForge.Models;

namespace MindForge.Services;

public interface IStorageService
{
    Task InitializeAsync();
    Task<IEnumerable<Subject>> GetSubjectsAsync();
    Task SaveSubjectAsync(Subject subject);
    Task DeleteSubjectAsync(Guid id);
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<UserProgress> GetUserProgressAsync();
    Task SaveUserProgressAsync(UserProgress progress);
    Task BackupAsync(string path);
    Task RestoreAsync(string path);
}
