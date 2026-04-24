using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MindForge.Services;

public class MindForgeDbContextFactory : IDesignTimeDbContextFactory<MindForgeDbContext>
{
    public MindForgeDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseSqlite($"Data Source={MindForgeDbContext.GetDbPath()}")
            .Options;
        return new MindForgeDbContext(opts);
    }
}
