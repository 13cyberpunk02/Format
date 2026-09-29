using Microsoft.EntityFrameworkCore;

namespace Format.Print.Api.Data;

public sealed class PrintDbContext(DbContextOptions<PrintDbContext> options) : DbContext(options)
{
    public DbSet<PrintOrder> Orders => Set<PrintOrder>();
    public DbSet<PrintJob> Jobs => Set<PrintJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<PrintOrder>();

        order.HasKey(o => o.Id);
        order.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
        order.Property(o => o.CreatedByName).HasMaxLength(200);
        order.Property(o => o.Error).HasMaxLength(2000);

        // Для очереди: «самый старый заказ в статусе Queued»
        order.HasIndex(o => new { o.Status, o.CreatedAt });
        // Для списка «мои заказы»
        order.HasIndex(o => new { o.CreatedById, o.CreatedAt });

        order.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        
        order.HasMany(o => o.Jobs)
            .WithOne()
            .HasForeignKey(j => j.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        var job = modelBuilder.Entity<PrintJob>();

        job.HasKey(j => j.Id);
        job.Property(j => j.Status).HasConversion<string>().HasMaxLength(32);
        job.Property(j => j.Printer).HasMaxLength(32);
        job.Property(j => j.Description).HasMaxLength(500);
        job.Property(j => j.StateMessage).HasMaxLength(500);
        job.Property(j => j.Error).HasMaxLength(2000);
        job.HasIndex(j => j.Status);

        var item = modelBuilder.Entity<PrintOrderItem>();

        item.HasKey(i => i.Id);
        item.Property(i => i.FileName).HasMaxLength(260);
        item.Property(i => i.Format).HasMaxLength(16);
    }
}