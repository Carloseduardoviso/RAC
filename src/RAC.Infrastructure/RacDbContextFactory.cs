using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RAC.Infrastructure;

// Design-time only. Production connection strings belong to environment variables or secret stores.
public sealed class RacDbContextFactory : IDesignTimeDbContextFactory<RacDbContext>
{
    public RacDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("RAC_SQLSERVER_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=RAC;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(RacDbContext).Assembly.FullName))
            .Options;
        return new RacDbContext(options);
    }
}
