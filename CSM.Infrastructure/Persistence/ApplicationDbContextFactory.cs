using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CSM.Infrastructure.Persistence;

/// <summary>
/// Creates the SQL Server model for EF tooling without starting the API host
/// or connecting to a database. Migration bundles receive their real
/// connection string when they are executed.
/// </summary>
public sealed class ApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection")
            ?? "Server=(local);Database=CSM_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
