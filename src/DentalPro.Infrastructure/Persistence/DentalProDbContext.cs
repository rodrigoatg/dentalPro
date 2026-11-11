using DentalPro.Domain.AccessControl;
using DentalPro.Infrastructure.Persistence.Configurations;
using DentalPro.Infrastructure.Persistence.Models;
using DentalPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.Infrastructure.Persistence;

public sealed class DentalProDbContext(DbContextOptions<DentalProDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PatientRow> Patients => Set<PatientRow>();
    public DbSet<MedicalRecordRow> MedicalRecords => Set<MedicalRecordRow>();
    public DbSet<OdontogramRow> Odontograms => Set<OdontogramRow>();
    public DbSet<AppointmentRow> Appointments => Set<AppointmentRow>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>("medical_record_number_seq", "public")
            .StartsAt(1)
            .IncrementsBy(1)
            .IsCyclic(false);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DentalProDbContext).Assembly);
    }
}
