using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Velqora.Infrastructure;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SocDb>
{
    public SocDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ??
            "Host=localhost;Database=velqora;Username=velqora;Password=design-time-only";
        return new SocDb(new DbContextOptionsBuilder<SocDb>().UseNpgsql(connection).Options);
    }
}
