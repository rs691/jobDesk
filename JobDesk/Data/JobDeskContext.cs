using JobDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace JobDesk.Data;

public class JobDeskContext : DbContext
{
    public JobDeskContext(DbContextOptions<JobDeskContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<WorkOrderMaterial> WorkOrderMaterials => Set<WorkOrderMaterial>();
    public DbSet<WorkOrderAttachment> WorkOrderAttachments => Set<WorkOrderAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkOrderAttachment>(a =>
        {
            a.Property(x => x.FileName).HasMaxLength(255);
            a.Property(x => x.ContentType).HasMaxLength(100);
            a.Property(x => x.StorageKey).HasMaxLength(400);
            a.Property(x => x.StorageProvider).HasMaxLength(200);
        });

        modelBuilder.Entity<Material>().Property(m => m.Name).HasMaxLength(100);
        modelBuilder.Entity<Material>().Property(m => m.Unit).HasMaxLength(20);

        // Store the enum as text so raw SQL can say WHERE Status = 'Open'.
        modelBuilder.Entity<WorkOrder>()
            .Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // SQLite has no decimal type; storing as REAL keeps SUM() math numeric in raw SQL.
        modelBuilder.Entity<Material>().Property(m => m.UnitCost).HasConversion<double>();
        modelBuilder.Entity<WorkOrderMaterial>().Property(m => m.Quantity).HasConversion<double>();

        modelBuilder.Entity<WorkOrder>()
            .HasOne(w => w.Customer)
            .WithMany(c => c.WorkOrders)
            .HasForeignKey(w => w.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkOrder>().HasIndex(w => new { w.CustomerId, w.Status });
    }
}
