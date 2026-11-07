using DentalPro.Domain.AccessControl;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(assignment => new { assignment.RoleId, assignment.PermissionId })
            .HasName("PK_RolePermissions");
        builder.Property(assignment => assignment.RoleId).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(assignment => assignment.PermissionId).HasColumnType("uuid").ValueGeneratedNever();

        builder.HasOne<IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RolePermissions_AspNetRoles_RoleId");

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(assignment => assignment.PermissionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RolePermissions_Permissions_PermissionId");
    }
}
