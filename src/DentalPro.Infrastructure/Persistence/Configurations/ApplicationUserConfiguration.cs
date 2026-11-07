using DentalPro.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers");
        builder.Property(user => user.FirstName).HasColumnType("text").IsRequired();
        builder.Property(user => user.LastName).HasColumnType("text").IsRequired();
        builder.Property(user => user.IsActive).HasColumnType("boolean").IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(user => user.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(user => user.LastLoginAt).HasColumnType("timestamptz");
    }
}
