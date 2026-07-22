using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Skill.Service.Data;

public class SkillDbContextFactory : IDesignTimeDbContextFactory<SkillDbContext>
{
    public SkillDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SkillDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=attrition;Username=postgres;Password=postgres",
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", "skill"))
            .Options;
        return new SkillDbContext(options);
    }
}
