using CategoryManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CategoryManagement.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>("category_code_seq").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories", table =>
            {
                table.HasCheckConstraint("CK_categories_level", "\"level\" BETWEEN 1 AND 3");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.CategoryCode).HasColumnName("category_code").HasMaxLength(5).IsRequired();
            entity.HasIndex(x => x.CategoryCode).IsUnique();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ParentId).HasColumnName("parent_id");
            entity.Property(x => x.Level).HasColumnName("level").IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products", table =>
            {
                table.HasCheckConstraint("CK_products_price", "\"price\" >= 0");
                table.HasCheckConstraint("CK_products_stock", "\"stock_quantity\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(x => x.Price).HasColumnName("price").HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(1000);
            entity.Property(x => x.StockQuantity).HasColumnName("stock_quantity").HasDefaultValue(0);
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.CategoryId);
            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
