using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pharma.Data.Models;

namespace Pharma.Data;

public partial class PharmaContext : DbContext
{
    public PharmaContext(DbContextOptions<PharmaContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Articles> Articles { get; set; }

    public virtual DbSet<Categories> Categories { get; set; }

    public virtual DbSet<Consultations> Consultations { get; set; }

    public virtual DbSet<CustomerAccounts> CustomerAccounts { get; set; }

    public virtual DbSet<NewsletterSubscriptions> NewsletterSubscriptions { get; set; }

    public virtual DbSet<ProductGroups> ProductGroups { get; set; }

    public virtual DbSet<ProductImages> ProductImages { get; set; }

    public virtual DbSet<Products> Products { get; set; }

    public virtual DbSet<SiteAssets> SiteAssets { get; set; }

    public virtual DbSet<SitePages> SitePages { get; set; }

    public virtual DbSet<StoreOrderItems> StoreOrderItems { get; set; }

    public virtual DbSet<StoreOrders> StoreOrders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_100_CI_AI");

        modelBuilder.Entity<Articles>(entity =>
        {
            entity.HasKey(e => e.Slug).HasName("PK__Articles__BC7B5FB70237268B");

            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.AuthorName)
                .HasMaxLength(200)
                .HasDefaultValue("Trí Bình Pharma", "DF_Articles_AuthorName");
            entity.Property(e => e.BodyHtml).HasDefaultValue("", "DF_Articles_BodyHtml");
            entity.Property(e => e.ImagePath).HasMaxLength(400);
            entity.Property(e => e.Section).HasMaxLength(60);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(300);
        });

        modelBuilder.Entity<Categories>(entity =>
        {
            entity.HasKey(e => e.Slug).HasName("PK__Categori__BC7B5FB7B2AA341D");

            entity.Property(e => e.Slug)
                .HasMaxLength(120)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Consultations>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Address).HasMaxLength(350);
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("(sysutcdatetime())", "DF_Consultations_CreatedUtc");
            entity.Property(e => e.CustomerName).HasMaxLength(120);
            entity.Property(e => e.Phone).HasMaxLength(25);
            entity.Property(e => e.ProductSlug)
                .HasMaxLength(160)
                .UseCollation("Latin1_General_100_BIN2");
        });

        modelBuilder.Entity<CustomerAccounts>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "UQ_CustomerAccounts_Email").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.NormalizedEmail)
                .HasMaxLength(254)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
        });

        modelBuilder.Entity<NewsletterSubscriptions>(entity =>
        {
            entity.HasKey(e => e.Email);

            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("(sysutcdatetime())", "DF_Newsletter_CreatedUtc");
        });

        modelBuilder.Entity<ProductGroups>(entity =>
        {
            entity.HasKey(e => e.Path);

            entity.Property(e => e.Path)
                .HasMaxLength(260)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);

            entity.HasMany(d => d.ProductSlug).WithMany(p => p.GroupPath)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductGroupItems",
                    r => r.HasOne<Products>().WithMany()
                        .HasForeignKey("ProductSlug")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_GroupItems_Products"),
                    l => l.HasOne<ProductGroups>().WithMany()
                        .HasForeignKey("GroupPath")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_GroupItems_Groups"),
                    j =>
                    {
                        j.HasKey("GroupPath", "ProductSlug");
                        j.IndexerProperty<string>("GroupPath")
                            .HasMaxLength(260)
                            .UseCollation("Latin1_General_100_BIN2");
                        j.IndexerProperty<string>("ProductSlug")
                            .HasMaxLength(160)
                            .UseCollation("Latin1_General_100_BIN2");
                    });
        });

        modelBuilder.Entity<ProductImages>(entity =>
        {
            entity.HasKey(e => new { e.ProductSlug, e.SortOrder });

            entity.Property(e => e.ProductSlug)
                .HasMaxLength(160)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.Path).HasMaxLength(400);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);

            entity.HasOne(d => d.ProductSlugNavigation).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductSlug)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProductIm__Produ__5070F446");
        });

        modelBuilder.Entity<Products>(entity =>
        {
            entity.HasKey(e => e.Slug).HasName("PK__Products__BC7B5FB70AEF0E65");

            entity.HasIndex(e => new { e.CategorySlug, e.SortOrder }, "IX_Products_CategorySort");

            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.BodyHtml).HasDefaultValue("", "DF_Products_BodyHtml");
            entity.Property(e => e.CategorySlug)
                .HasMaxLength(120)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.Distributor).HasMaxLength(250);
            entity.Property(e => e.Manufacturer).HasMaxLength(250);
            entity.Property(e => e.Name).HasMaxLength(300);
            entity.Property(e => e.Origin).HasMaxLength(250);
            entity.Property(e => e.Packaging).HasMaxLength(250);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);

            entity.HasOne(d => d.CategorySlugNavigation).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategorySlug)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Products__Catego__4CA06362");
        });

        modelBuilder.Entity<SiteAssets>(entity =>
        {
            entity.HasKey(e => e.AssetKey).HasName("PK__SiteAsse__B20803080062DED6");

            entity.Property(e => e.AssetKey)
                .HasMaxLength(80)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.AltText).HasMaxLength(300);
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.Path).HasMaxLength(400);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<SitePages>(entity =>
        {
            entity.HasKey(e => e.Slug);

            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(300);
        });

        modelBuilder.Entity<StoreOrderItems>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.ProductSlug });

            entity.Property(e => e.ProductSlug)
                .HasMaxLength(160)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.ProductName).HasMaxLength(300);

            entity.HasOne(d => d.Order).WithMany(p => p.StoreOrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoreOrderItems_Order");
        });

        modelBuilder.Entity<StoreOrders>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Address).HasMaxLength(350);
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("(sysutcdatetime())", "DF_StoreOrders_CreatedUtc");
            entity.Property(e => e.CustomerName).HasMaxLength(120);
            entity.Property(e => e.Note).HasMaxLength(600);
            entity.Property(e => e.Phone).HasMaxLength(25);

            entity.HasOne(d => d.Account).WithMany(p => p.StoreOrders)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK_StoreOrders_Account");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
