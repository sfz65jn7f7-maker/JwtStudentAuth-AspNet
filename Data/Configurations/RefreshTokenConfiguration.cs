using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentAuth.Api.Models;

namespace StudentAuth.Api.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token).IsRequired().HasMaxLength(200);
        builder.HasIndex(r => r.Token).IsUnique();

        builder.HasOne(r => r.Student)
               .WithMany(s => s.RefreshTokens)
               .HasForeignKey(r => r.StudentId)
               .OnDelete(DeleteBehavior.Cascade); // supprimer un compte supprime ses refresh tokens
    }
}