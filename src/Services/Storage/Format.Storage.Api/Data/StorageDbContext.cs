using Microsoft.EntityFrameworkCore;

namespace Format.Storage.Api.Data;

public sealed class StorageDbContext(DbContextOptions<StorageDbContext> options) : DbContext(options)
{
    public DbSet<Drawing> Drawings => Set<Drawing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var drawing = modelBuilder.Entity<Drawing>();

        drawing.HasKey(d => d.Id);
        drawing.HasIndex(d => d.UploadId);
        drawing.Property(d => d.FileName).HasMaxLength(260);
        drawing.Property(d => d.FormatName).HasMaxLength(16);
        drawing.Property(d => d.UploadedBy).HasMaxLength(128);

        // Список чертежей чаще всего сортируется по дате загрузки
        drawing.HasIndex(d => d.UploadedAt);
    }
}