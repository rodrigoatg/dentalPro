using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DentalPro.Infrastructure.Persistence;

public sealed class DentalProDbContextFactory : IDesignTimeDbContextFactory<DentalProDbContext>
{
    public DentalProDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DentalPro")
            ?? "Host=localhost;Port=5432;Database=dentalpro_dev";

        var options = new DbContextOptionsBuilder<DentalProDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DentalProDbContext(options);
    }
}
