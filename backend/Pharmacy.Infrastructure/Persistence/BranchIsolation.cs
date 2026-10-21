using Microsoft.EntityFrameworkCore;
using Pharmacy.Domain.Entities;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;
using Pharmacy.Domain.Entities.Purchasing;
using Pharmacy.Domain.Entities.Payments;
using Pharmacy.Infrastructure.Identity;

namespace Pharmacy.Infrastructure.Persistence;

/// <summary>
/// Maps pharmacy and identity entities to SQL Server and applies branch ownership rules to operational records.
/// </summary>
public sealed partial class PharmacyDbContext
{
    private void ConfigureBranches(ModelBuilder model)
    {
        model.Entity<Branch>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(20); b.Property(x => x.Name).HasMaxLength(150);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasData(new Branch { Id = 1, Code = "MAIN", Name = "Main branch", IsActive = true });
        });
        model.Entity<ApplicationUser>().Property(x => x.BranchId).HasDefaultValue(1);
        model.Entity<ApplicationUser>().HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        Scope<MedicineBatch>(model); Scope<Sale>(model); Scope<SaleItem>(model); Scope<PurchaseInvoice>(model); Scope<PurchaseItem>(model);
        Scope<StockMovement>(model); Scope<Payment>(model); Scope<SaleReturn>(model); Scope<SaleReturnItem>(model); Scope<SaleRefund>(model);
        Scope<PharmacySettings>(model); Scope<ExpiryNotification>(model); Scope<AuditLog>(model); Scope<Customer>(model);
        Scope<Pharmacy.Domain.Entities.Gst.GstDocumentDetails>(model);
        Scope<Pharmacy.Domain.Entities.Gst.GstReturnWorkspace>(model);
        Scope<Pharmacy.Domain.Entities.Gst.GstReturnReview>(model);
        model.Entity<Pharmacy.Domain.Entities.Gst.GstDocumentDetails>(b => {
            b.Property(x => x.SourceKind).HasMaxLength(30); b.Property(x => x.UpdatedBy).HasMaxLength(450);
            b.Property(x => x.Revision).IsConcurrencyToken(); b.HasIndex("BranchId", "SourceKind", "SourceId").IsUnique();
        });
        model.Entity<Pharmacy.Domain.Entities.Gst.GstReturnWorkspace>(b => {
            b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.Revision).IsConcurrencyToken(); b.HasIndex("BranchId", "Year", "Month").IsUnique();
        });
        model.Entity<Pharmacy.Domain.Entities.Gst.GstReturnReview>(b => {
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ReviewedBy).HasMaxLength(450); b.HasIndex("BranchId", "Year", "Month");
        });
    }
    private void Scope<T>(ModelBuilder model) where T : class
    {
        model.Entity<T>().Property<int>("BranchId").HasDefaultValue(1);
        model.Entity<T>().HasOne<Branch>().WithMany().HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict);
        model.Entity<T>().HasQueryFilter(x => EF.Property<int>(x, "BranchId") == CurrentBranchId);
    }
    private void GuardBranchWrites()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.Metadata.ClrType != typeof(ApplicationUser) && e.Metadata.FindProperty("BranchId") is not null))
        {
            if (entry.State == EntityState.Added) entry.Property("BranchId").CurrentValue = CurrentBranchId;
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if ((int)entry.Property("BranchId").OriginalValue! != CurrentBranchId || (int)entry.Property("BranchId").CurrentValue! != CurrentBranchId)
                    throw new InvalidOperationException("Cross-branch changes are not allowed.");
            }
        }
    }
    /// <summary>
    /// Applies branch ownership validation before synchronously saving tracked changes; it rejects cross-branch modification or deletion.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { GuardBranchWrites(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    /// <summary>
    /// Applies branch ownership validation before persisting tracked changes, rejecting attempts to modify another branch&apos;s records.
    /// </summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { GuardBranchWrites(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
