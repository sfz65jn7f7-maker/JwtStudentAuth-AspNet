using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentAuth.Api.Models;

namespace StudentAuth.Api.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.LastName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Email).IsRequired().HasMaxLength(256);
        builder.Property(s => s.PasswordHash).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => s.Email).IsUnique();
    }
}