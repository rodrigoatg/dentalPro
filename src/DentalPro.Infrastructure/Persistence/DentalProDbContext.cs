using DentalPro.Infrastructure.Persistence.Configurations;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.Infrastructure.Persistence;

public sealed class DentalProDbContext(DbContextOptions<DentalProDbContext> options) : DbContext(options)
{
    public DbSet<PatientRow> Patients => Set<PatientRow>();
    public DbSet<MedicalRecordRow> MedicalRecords => Set<MedicalRecordRow>();
    public DbSet<OdontogramRow> Odontograms => Set<OdontogramRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("medical_record_number_seq", "public")
            .StartsAt(1)
            .IncrementsBy(1)
            .IsCyclic(false);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DentalProDbContext).Assembly);
    }
}
