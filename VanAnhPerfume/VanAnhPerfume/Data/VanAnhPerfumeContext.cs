using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Data;

public partial class VanAnhPerfumeContext : DbContext
{
    public VanAnhPerfumeContext()
    {
    }

    public VanAnhPerfumeContext(DbContextOptions<VanAnhPerfumeContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AddressBook> AddressBooks { get; set; }
    public virtual DbSet<AddToCartLog> AddToCartLogs { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<AdminNotification> AdminNotifications { get; set; }

    public virtual DbSet<BatchInventory> BatchInventories { get; set; }

    public virtual DbSet<Banner> Banners { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<FragranceNote> FragranceNotes { get; set; }

    public virtual DbSet<News> News { get; set; }

    public virtual DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }
    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }
    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderStatusLog> OrderStatusLogs { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<OrderGifting> OrderGiftings { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<PaymentLog> PaymentLogs { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }
    public virtual DbSet<VariantAttribute> VariantAttributes { get; set; }
    public virtual DbSet<VariantAttributeValue> VariantAttributeValues { get; set; }
    public virtual DbSet<ProductVariantAttributeValue> ProductVariantAttributeValues { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AddressBook>(entity =>
        {
            entity.HasKey(e => e.AddressId).HasName("PK__AddressB__091C2AFB843B15CC");

            entity.ToTable("AddressBook");

            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.ReceiverName).HasMaxLength(100);
            entity.Property(e => e.StreetAddress).HasMaxLength(255);
            entity.Property(e => e.Ward).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.AddressBooks)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AddressBook_Users");
        });

        modelBuilder.Entity<AddToCartLog>(entity =>
        {
            entity.ToTable("AddToCartLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.ActionType).HasMaxLength(50);
            entity.Property(e => e.ChangedBy).HasMaxLength(150);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<AdminNotification>(entity =>
        {
            entity.ToTable("AdminNotifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.Type).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
        });

        modelBuilder.Entity<BatchInventory>(entity =>
        {
            entity.HasKey(e => e.BatchId).HasName("PK__BatchInv__5D55CE58951FD543");

            entity.ToTable("BatchInventory");

            entity.Property(e => e.BatchNumber).HasMaxLength(50);

            entity.HasOne(d => d.Variant).WithMany(p => p.BatchInventories)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batch_Variant");
        });

        modelBuilder.Entity<Banner>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.SubTitle).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.MobileImageUrl).HasMaxLength(500);
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.ButtonText).HasMaxLength(100).HasDefaultValue("Khám phá ngay");
            entity.Property(e => e.TextColor).HasMaxLength(7).HasDefaultValue("#FFFFFF");
            entity.Property(e => e.Position).HasMaxLength(50).HasDefaultValue("hero");
            entity.Property(e => e.MetaTitle).HasMaxLength(255);
            entity.Property(e => e.MetaDescription).HasMaxLength(500);
            entity.Property(e => e.PublishAt).HasColumnType("datetime");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasKey(e => e.BrandId).HasName("PK__Brands__DAD4F05ED76C58F7");

            entity.HasIndex(e => e.Name, "UQ__Brands__737584F60BCECE33").IsUnique();

            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.Logo).HasMaxLength(255);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.BannerUrl).HasMaxLength(500);
            entity.Property(e => e.CountryOfOrigin).HasMaxLength(100);
            entity.Property(e => e.Website).HasMaxLength(255);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Slug).HasMaxLength(255);
            entity.Property(e => e.MetaTitle).HasMaxLength(255);
            entity.Property(e => e.MetaDescription).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B78E261A93");

            entity.ToTable("Cart");

            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.User).WithMany(p => p.Carts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cart_Users");

            entity.HasOne(d => d.Variant).WithMany(p => p.Carts)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cart_Variants");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0BC29C9CA2");

            entity.HasIndex(e => e.Name, "UQ__Categori__737584F62DFF8504").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.MetaTitle).HasMaxLength(255);
            entity.Property(e => e.MetaDescription).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<FragranceNote>(entity =>
        {
            entity.HasKey(e => e.NoteId).HasName("PK__Fragranc__EACE355F45BB1FDA");

            entity.HasIndex(e => e.Name, "UQ__Fragranc__737584F660CE370D").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Type).HasMaxLength(50);
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.DiscountType).HasMaxLength(20);
            entity.Property(e => e.Value).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MinOrderValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.ApplicableCategoryIds).HasMaxLength(500);
            entity.Property(e => e.ApplicableProductIds).HasMaxLength(1000);
            entity.Property(e => e.AutoApply).HasDefaultValue(false);
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasKey(e => e.NewsId).HasName("PK__News__954EBDF3BF685EB9");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Image).HasMaxLength(255);
            entity.Property(e => e.Excerpt).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AuthorName).HasMaxLength(200);
            entity.Property(e => e.CategoryTag).HasMaxLength(100);
            entity.Property(e => e.NewsStatus).HasMaxLength(20);
            entity.Property(e => e.Slug).HasMaxLength(255);
            entity.Property(e => e.MetaTitle).HasMaxLength(255);
            entity.Property(e => e.MetaDescription).HasMaxLength(2000);
            entity.Property(e => e.PublishAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ViewCount).HasDefaultValue(0);
            entity.Property(e => e.IsFeatured).HasDefaultValue(false);
            entity.Property(e => e.Status).HasDefaultValue(true);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(255);
            entity.Property(e => e.Title).HasMaxLength(255);
        });

        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.ToTable("InventoryLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActionType).HasMaxLength(30);
            entity.Property(e => e.ChangeType).HasMaxLength(30).HasDefaultValue("Adjustment");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.ToTable("Inventories");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductVariantId).IsUnique();
            entity.Property(e => e.LowStockThreshold).HasDefaultValue(5);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.HasOne(e => e.ProductVariant)
                .WithOne(v => v.Inventory)
                .HasForeignKey<Inventory>(e => e.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NewsletterSubscriber>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.SubscribedAt).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF795320E8");

            entity.HasIndex(e => e.OrderCode).IsUnique();
            entity.Property(e => e.OrderCode).HasMaxLength(30);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.CustomerEmail).HasMaxLength(150);
            entity.Property(e => e.ShippingProvince).HasMaxLength(100);
            entity.Property(e => e.ShippingDistrict).HasMaxLength(100);
            entity.Property(e => e.CouponCode).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Unpaid");
            entity.Property(e => e.ShippingStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TrackingCode).HasMaxLength(100);
            entity.Property(e => e.AdminNote).HasMaxLength(1000);
            entity.Property(e => e.RefundStatus).HasMaxLength(50);
            entity.Property(e => e.ConfirmationEmailSentAt).HasColumnType("datetime");
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ShippingFee).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Users");
        });

        modelBuilder.Entity<OrderStatusLog>(entity =>
        {
            entity.ToTable("OrderStatusLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OldStatus).HasMaxLength(50);
            entity.Property(e => e.NewStatus).HasMaxLength(50);
            entity.Property(e => e.OldPaymentStatus).HasMaxLength(50);
            entity.Property(e => e.NewPaymentStatus).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.ChangedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__D3B9D36C93FB879E");

            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.VariantName).HasMaxLength(255);
            entity.Property(e => e.Sku).HasMaxLength(100);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_OrderDetails_Orders");

            entity.HasOne(d => d.Variant).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderDetails_Variants");
        });

        modelBuilder.Entity<OrderGifting>(entity =>
        {
            entity.HasKey(e => e.GiftingId).HasName("PK__OrderGif__780BBDFFA539C87E");

            entity.ToTable("OrderGifting");

            entity.HasIndex(e => e.OrderId, "UQ__OrderGif__C3905BCEBE22548C").IsUnique();

            entity.Property(e => e.GiftMessage).HasMaxLength(500);
            entity.Property(e => e.IncludeGiftWrap).HasDefaultValue(false);

            entity.HasOne(d => d.Order).WithOne(p => p.OrderGifting)
                .HasForeignKey<OrderGifting>(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderGifting_Orders");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A380FCEECA4");

            entity.HasIndex(e => e.TransactionId);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaidAt).HasColumnType("datetime");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasDefaultValue("VietQR");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            entity.Property(e => e.PaymentGatewayResponse).HasMaxLength(4000);
            entity.Property(e => e.RefundedAt).HasColumnType("datetime");
            entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RefundNote).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Orders");
        });

        modelBuilder.Entity<PaymentLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__PaymentL__5E548648A42E2BA5");

            entity.Property(e => e.LogDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Message).HasMaxLength(255);
            entity.Property(e => e.OldStatus).HasMaxLength(30);
            entity.Property(e => e.NewStatus).HasMaxLength(30);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Source).HasMaxLength(30);

            entity.HasOne(d => d.Payment).WithMany(p => p.PaymentLogs)
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentLogs_Payments");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CD1CACA511");

            entity.Property(e => e.Concentration).HasMaxLength(50);
            entity.Property(e => e.Gender).HasMaxLength(20);
            entity.Property(e => e.IsFeatured).HasDefaultValue(false);
            entity.Property(e => e.MainImage).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Status).HasDefaultValue(true);
            entity.Property(e => e.ProductStatus).HasMaxLength(20).HasDefaultValue("Active");
            entity.Property(e => e.Slug).HasMaxLength(255);
            entity.Property(e => e.MetaTitle).HasMaxLength(255);
            entity.Property(e => e.MetaDescription).HasMaxLength(500);
            entity.Property(e => e.RelatedProductIds).HasMaxLength(1000);
            entity.Property(e => e.AverageRating).HasColumnType("decimal(3,1)");
            entity.Property(e => e.ReviewCount).HasDefaultValue(0);
            entity.Property(e => e.ShortDescription).HasMaxLength(1000);
            entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
            entity.Property(e => e.DetailSpecsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Brand).WithMany(p => p.Products)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Brands");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Categories");

            entity.HasMany(d => d.Notes).WithMany(p => p.Products)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductNote",
                    r => r.HasOne<FragranceNote>().WithMany()
                        .HasForeignKey("NoteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductNotes_Notes"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductNotes_Products"),
                    j =>
                    {
                        j.HasKey("ProductId", "NoteId");
                        j.ToTable("ProductNotes");
                    });

            entity.HasMany(d => d.Promos).WithMany(p => p.Products)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductPromotion",
                    r => r.HasOne<Promotion>().WithMany()
                        .HasForeignKey("PromoId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProdPromo_Promos"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProdPromo_Products"),
                    j =>
                    {
                        j.HasKey("ProductId", "PromoId");
                        j.ToTable("ProductPromotions");
                    });
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PK__ProductI__7516F70C3E582BC6");

            entity.Property(e => e.ImageUrl).HasMaxLength(255);
            entity.Property(e => e.IsPrimary).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_ProductImages_Products");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.VariantId).HasName("PK__ProductV__0EA233848924DB15");

            entity.HasIndex(e => e.Sku, "UQ__ProductV__CA1ECF0DF22CA0C7").IsUnique();

            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Size).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Material).HasMaxLength(100);
            entity.Property(e => e.VariantImageUrl).HasMaxLength(500);
            entity.Property(e => e.Sku)
                .HasMaxLength(200)
                .HasColumnName("SKU");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_ProductVariants_Products");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.PromoId).HasName("PK__Promotio__33D334B0211FD6BC");

            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.MinOrderValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ApplicableCategoryIds).HasMaxLength(500);
            entity.Property(e => e.ApplicableProductIds).HasMaxLength(1000);
            entity.Property(e => e.AutoApply).HasDefaultValue(false);
        });

        modelBuilder.Entity<VariantAttribute>(entity =>
        {
            entity.ToTable("VariantAttributes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.HasOne(e => e.Product)
                .WithMany(p => p.VariantAttributes)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VariantAttributeValue>(entity =>
        {
            entity.ToTable("VariantAttributeValues");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).HasMaxLength(100);
            entity.HasOne(e => e.VariantAttribute)
                .WithMany(a => a.Values)
                .HasForeignKey(e => e.VariantAttributeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductVariantAttributeValue>(entity =>
        {
            entity.ToTable("ProductVariantAttributeValues");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.ProductVariant)
                .WithMany(v => v.ProductVariantAttributeValues)
                .HasForeignKey(e => e.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.VariantAttributeValue)
                .WithMany(v => v.ProductVariantAttributeValues)
                .HasForeignKey(e => e.VariantAttributeValueId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__Reviews__74BC79CE5EF3C96E");

            entity.Property(e => e.ReviewerName).HasMaxLength(200);
            entity.Property(e => e.ReviewerEmail).HasMaxLength(200);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.AdminReply).HasMaxLength(1000);
            entity.Property(e => e.AdminReplyAt).HasColumnType("datetime");
            entity.Property(e => e.HelpfulCount).HasDefaultValue(0);
            entity.Property(e => e.IsVerifiedPurchase).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Product).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reviews_Products");

            entity.HasOne(d => d.User).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Reviews_Users");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1AF4DCE2C8");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B616075D275F7").IsUnique();

            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__Transact__55433A6BC63ED18E");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TransactionType).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Transactions_Orders");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C5D9B016A");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534707D28CA").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Status).HasDefaultValue(true);
            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.IsEmailVerified).HasDefaultValue(false);
            entity.Property(e => e.LastLoginAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DateOfBirth).HasColumnType("datetime");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(200);
            entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
            entity.HasOne(e => e.User)
                .WithMany(x => x.PasswordResetTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
