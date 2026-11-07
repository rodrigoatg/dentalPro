using DentalPro.Domain.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(permission => permission.Id).HasName("PK_Permissions");
        builder.Property(permission => permission.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(permission => permission.Code).HasColumnType("text").IsRequired();
        builder.Property(permission => permission.Name).HasColumnType("text").IsRequired();
        builder.Property(permission => permission.Description).HasColumnType("text");
        builder.Property(permission => permission.Module).HasColumnType("text").IsRequired();

        builder.HasIndex(permission => permission.Code)
            .IsUnique()
            .HasDatabaseName("UX_Permissions_Code");
    }
}
