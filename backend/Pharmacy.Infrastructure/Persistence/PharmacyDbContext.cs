using Microsoft.EntityFrameworkCore;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Domain.Entities.Sales;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Domain.Entities.Payments;
namespace Pharmacy.Infrastructure.Persistence;

/// <summary>
/// Maps pharmacy and identity entities to SQL Server and applies branch ownership rules to operational records.
/// </summary>
public sealed partial class PharmacyDbContext(
    DbContextOptions<PharmacyDbContext> options,
    Pharmacy.Application.Branches.IBranchContext? branchContext = null)
    : IdentityDbContext<ApplicationUser>(options)
{
    /// <summary>
    /// Current scoped branch used in query filters and write ownership validation.
    /// </summary>
    public int CurrentBranchId => branchContext?.BranchId ?? 1;
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Pharmacy.Domain.Entities.Gst.GstDocumentDetails> GstDocumentDetails => Set<Pharmacy.Domain.Entities.Gst.GstDocumentDetails>();
    public DbSet<Pharmacy.Domain.Entities.Gst.GstReturnWorkspace> GstReturnWorkspaces => Set<Pharmacy.Domain.Entities.Gst.GstReturnWorkspace>();
    public DbSet<Pharmacy.Domain.Entities.Gst.GstReturnReview> GstReturnReviews => Set<Pharmacy.Domain.Entities.Gst.GstReturnReview>();
    
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicineBatch> MedicineBatches =>Set<MedicineBatch>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ExpiryNotification> ExpiryNotifications => Set<ExpiryNotification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PharmacySettings> PharmacySettings => Set<PharmacySettings>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Pharmacy.Domain.Entities.Sales.Returns.SaleReturn> SaleReturns => Set<Pharmacy.Domain.Entities.Sales.Returns.SaleReturn>();
    public DbSet<Pharmacy.Domain.Entities.Sales.Returns.SaleReturnItem> SaleReturnItems => Set<Pharmacy.Domain.Entities.Sales.Returns.SaleReturnItem>();
    public DbSet<Pharmacy.Domain.Entities.Sales.Returns.SaleRefund> SaleRefunds => Set<Pharmacy.Domain.Entities.Sales.Returns.SaleRefund>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new SaleReturnConfiguration());
        modelBuilder.ApplyConfiguration(new SaleReturnItemConfiguration());
        modelBuilder.ApplyConfiguration(new SaleRefundConfiguration());
        ConfigureBranches(modelBuilder);

        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.ToTable("Medicines");

            entity.HasKey(m => m.Id);

            entity.Property(m => m.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(m => m.GenericName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(m => m.Manufacturer)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(m => m.Category)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(m => m.IsActive)
                .HasDefaultValue(true);

            // Step 21: medicine GST configuration.
            entity.Property(m => m.HsnCode).HasMaxLength(20);
            entity.Property(m => m.GstPercentage)
                .HasPrecision(5, 2)
                .HasDefaultValue(0m);
        });
        modelBuilder.Entity<MedicineBatch>(entity =>
        {
            entity.ToTable("MedicineBatches");

            entity.HasKey(b => b.Id);

            entity.Property(b => b.BatchNumber)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(b => b.ExpiryDate)
                .HasColumnType("date");

            entity.Property(b => b.PurchasePrice)
                .HasPrecision(18, 2);

            entity.Property(b => b.SellingPrice)
                .HasPrecision(18, 2);

            entity.Property(b => b.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(b => b.Medicine)
                .WithMany()
                .HasForeignKey(b => b.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(b => new
            {
                b.MedicineId,
                b.BatchNumber
            }).IsUnique();

            entity.HasIndex(b => b.ExpiryDate);
        });
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Address).HasMaxLength(500);

            entity.HasIndex(x => x.Name);
        });
        modelBuilder.Entity<PurchaseInvoice>()
        .Navigation(x => x.Items)
        .UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.ToTable("PurchaseInvoices");
            

            entity.HasKey(x => x.Id);

            entity.Property(x => x.InvoiceNumber)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.InvoiceDate)
                .HasColumnType("date");

            entity.Property(x => x.Status)
                .HasConversion<int>();

            entity.Property(x => x.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SupplierId, x.InvoiceNumber })
                .IsUnique();
            entity.Property(x => x.RowVersion)
                .IsRowVersion();
                
        });

        modelBuilder.Entity<PurchaseItem>(entity =>
        {
            entity.ToTable("PurchaseItems");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.BatchNumber)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ExpiryDate)
                .HasColumnType("date");

            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            entity.Property(x => x.SellingPrice).HasPrecision(18, 2);

            entity.HasOne(x => x.PurchaseInvoice)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Medicine>()
                .WithMany()
                .HasForeignKey(x => x.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("StockMovements");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.MovementType)
                .HasConversion<int>();

            entity.Property(x => x.Reference)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Notes)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(x => x.MedicineBatch)
                .WithMany()
                .HasForeignKey(x => x.MedicineBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.MedicineBatchId, x.CreatedAtUtc });
        });
        
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("Sales");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.InvoiceNumber)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.InvoiceNumber)
                .IsUnique();

            entity.Property(x => x.TotalAmount)
                .HasPrecision(18, 2);

            // Step 21: inclusive GST summary amounts.
            entity.Property(x => x.SubtotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxableAmount).HasPrecision(18, 2);
            entity.Property(x => x.GstAmount).HasPrecision(18, 2);

            entity.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(x => x.Status)
                .HasConversion<int>();

            entity.Navigation(x => x.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.ToTable("SaleItems");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.UnitPrice)
                .HasPrecision(18, 2);

            entity.Property(x => x.LineTotal)
                .HasPrecision(18, 2);

            // Step 21: snapshot and GST-inclusive line breakdown.
            entity.Property(x => x.MedicineNameSnapshot)
                .HasMaxLength(150)
                .IsRequired()
                .HasDefaultValue("");
            entity.Property(x => x.HsnCode).HasMaxLength(20);
            entity.Property(x => x.GstPercentage).HasPrecision(5, 2);
            entity.Property(x => x.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(x => x.GrossAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxableAmount).HasPrecision(18, 2);
            entity.Property(x => x.GstAmount).HasPrecision(18, 2);

            entity.HasOne(x => x.Sale)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Medicine>()
                .WithMany()
                .HasForeignKey(x => x.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<MedicineBatch>()
                .WithMany()
                .HasForeignKey(x => x.MedicineBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.MedicineBatchId);
        });
        
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.Phone)
                    .HasMaxLength(20);

                entity.Property(x => x.Email)
                    .HasMaxLength(254);

                entity.Property(x => x.Address)
                    .HasMaxLength(500);

                entity.Property(x => x.CreatedAtUtc)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.HasIndex(x => x.Phone);
            });
            
            modelBuilder.Entity<ExpiryNotification>(entity =>
            {
                entity.ToTable("ExpiryNotifications");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.AlertType)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Message)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(x => x.CreatedAtUtc)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.IsRead)
                    .HasDefaultValue(false);

                entity.HasOne<MedicineBatch>()
                    .WithMany()
                    .HasForeignKey(x => x.MedicineBatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                // One notification per batch and alert category.
                entity.HasIndex(x => new
                {
                    x.MedicineBatchId,
                    x.AlertType
                }).IsUnique();
            });
            
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.UserId)
                    .HasMaxLength(450);

                entity.Property(x => x.UserName)
                    .HasMaxLength(256)
                    .IsRequired();

                entity.Property(x => x.Action)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.EntityType)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.EntityId)
                    .HasMaxLength(100);

                entity.Property(x => x.OldValues)
                    .HasColumnType("nvarchar(max)");

                entity.Property(x => x.NewValues)
                    .HasColumnType("nvarchar(max)");

                entity.HasIndex(x => x.CreatedAtUtc);

                entity.HasIndex(x => new
                {
                    x.EntityType,
                    x.EntityId
                });
            });

            modelBuilder.Entity<PharmacySettings>(entity =>
            {
                entity.ToTable("PharmacySettings");

                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id)
                    .ValueGeneratedOnAdd();
                entity.Property(x => x.Revision).IsConcurrencyToken();
                entity.HasIndex(x => x.SingletonKey).IsUnique();
                entity.Property(x => x.ReceiptFooter).HasMaxLength(500);
                entity.Property(x => x.PharmacyName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Address)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(x => x.PhoneNumber)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.Email)
                    .HasMaxLength(150);

                entity.Property(x => x.Gstin)
                    .HasMaxLength(15);

                entity.Property(x => x.InvoicePrefix)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.DefaultGstPercentage)
                    .HasPrecision(5, 2);
            });
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("Payments");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Amount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.Method)
                    .HasConversion<int>();

                entity.Property(x => x.Reference)
                    .HasMaxLength(150);

                entity.Property(x => x.IdempotencyKey)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.CreatedAtUtc)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasOne<Sale>()
                    .WithMany(x => x.Payments)
                    .HasForeignKey(x => x.SaleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.SaleId);

                entity.HasIndex(x => x.IdempotencyKey)
                    .IsUnique();
            });
            modelBuilder.Entity<MedicineBatch>().HasIndex(x => new { x.MedicineId, x.BatchNumber }).IsUnique(false);
            modelBuilder.Entity<MedicineBatch>().HasIndex("BranchId", nameof(MedicineBatch.MedicineId), nameof(MedicineBatch.BatchNumber)).IsUnique();
            modelBuilder.Entity<Sale>().HasIndex(x => x.InvoiceNumber).IsUnique(false);
            modelBuilder.Entity<Sale>().HasIndex("BranchId", nameof(Sale.InvoiceNumber)).IsUnique();
            modelBuilder.Entity<Sale>().Property(x => x.CheckoutFingerprint).HasMaxLength(64);
            modelBuilder.Entity<Sale>().Property(x => x.CancellationReason).HasMaxLength(500);
            modelBuilder.Entity<Sale>().Property(x => x.CancelledByUserId).HasMaxLength(450);
            modelBuilder.Entity<Sale>().Property(x => x.CancellationFingerprint).HasMaxLength(64);
            modelBuilder.Entity<Sale>().HasIndex("BranchId", nameof(Sale.CheckoutKey)).IsUnique().HasFilter("[CheckoutKey] IS NOT NULL");
            modelBuilder.Entity<PurchaseInvoice>().HasIndex(x => new { x.SupplierId, x.InvoiceNumber }).IsUnique(false);
            modelBuilder.Entity<PurchaseInvoice>().HasIndex("BranchId", nameof(PurchaseInvoice.SupplierId), nameof(PurchaseInvoice.InvoiceNumber)).IsUnique();
            modelBuilder.Entity<PharmacySettings>().HasIndex(x => x.SingletonKey).IsUnique(false);
            modelBuilder.Entity<PharmacySettings>().HasIndex("BranchId", "SingletonKey").IsUnique();
            modelBuilder.Entity<PharmacySettings>().Property(x => x.UpiId).HasMaxLength(100);
            modelBuilder.Entity<PharmacySettings>().Property(x => x.UpiPayeeName).HasMaxLength(100);
            modelBuilder.Entity<PharmacySettings>().Property(x => x.UpiMerchantCode).HasMaxLength(4);
            modelBuilder.Entity<Payment>().HasIndex(x => x.IdempotencyKey).IsUnique(false);
            modelBuilder.Entity<Payment>().HasIndex("BranchId", nameof(Payment.IdempotencyKey)).IsUnique();

    }
}
