using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRow>
{
    public void Configure(EntityTypeBuilder<AuditEventRow> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(entry => entry.Id).HasName("PK_AuditEvents");

        builder.Property(entry => entry.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(entry => entry.ActorId).HasColumnType("uuid").IsRequired();
        builder.Property(entry => entry.EntityType).HasColumnType("text").IsRequired();
        builder.Property(entry => entry.EntityId).HasColumnType("uuid").IsRequired();
        builder.Property(entry => entry.Action).HasColumnType("text").IsRequired();
        builder.Property(entry => entry.OccurredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(entry => entry.RelatedEntityId).HasColumnType("uuid");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(entry => entry.ActorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AuditEvents_AspNetUsers_ActorId");

        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId, entry.OccurredAt })
            .HasDatabaseName("IX_AuditEvents_EntityType_EntityId_OccurredAt");
        builder.HasIndex(entry => new { entry.ActorId, entry.OccurredAt })
            .HasDatabaseName("IX_AuditEvents_ActorId_OccurredAt");
    }
}
