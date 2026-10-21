using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Entities.Sales;
using Pharmacy.Domain.Entities.Sales.Returns;

namespace Pharmacy.Infrastructure.Persistence;

/// <summary>
/// Configures persistence relationships and monetary fields for original-sale-linked return headers.
/// </summary>
public sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    /// <summary>
    /// Maps return headers, original sale relationships and persisted monetary values for Entity Framework.
    /// </summary>
    public void Configure(EntityTypeBuilder<SaleReturn> entity)
    {
        entity.ToTable("SaleReturns");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        entity.Ignore(x => x.TotalAmount);
        entity.HasIndex(x => new { x.SaleId, x.IdempotencyKey }).IsUnique();
        entity.HasOne<Sale>().WithMany(x => x.Returns).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
    }
}
/// <summary>
/// Configures returned-line persistence, original sale-item links and inspection-related values.
/// </summary>
public sealed class SaleReturnItemConfiguration : IEntityTypeConfiguration<SaleReturnItem>
{
    /// <summary>
    /// Maps original-line return references, disposition, inspection fields and credit precision for Entity Framework.
    /// </summary>
    public void Configure(EntityTypeBuilder<SaleReturnItem> entity)
    {
        entity.ToTable("SaleReturnItems", table => table.HasCheckConstraint("CK_SaleReturnItems_Disposition", "[Disposition] IN (1,2,3,4,5) AND ([Restock] = 0 OR [Disposition] = 1)"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.InspectedByUserId).HasMaxLength(450);
        entity.HasIndex(x => new { x.SaleReturnId, x.SaleItemId }).IsUnique();
        entity.HasOne<SaleItem>().WithMany().HasForeignKey(x => x.SaleItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
/// <summary>
/// Configures refund ledger persistence, sale/return links and payout retry identity.
/// </summary>
public sealed class SaleRefundConfiguration : IEntityTypeConfiguration<SaleRefund>
{
    /// <summary>
    /// Maps refund sale/return relationships, monetary precision and retry identity for Entity Framework.
    /// </summary>
    public void Configure(EntityTypeBuilder<SaleRefund> entity)
    {
        entity.ToTable("SaleRefunds");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Reference).HasMaxLength(150);
        entity.HasIndex(x => new { x.SaleId, x.IdempotencyKey }).IsUnique();
        entity.HasOne<Sale>().WithMany(x => x.Refunds).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<SaleReturn>().WithMany().HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.SaleReturnId).IsUnique().HasFilter("[SaleReturnId] IS NOT NULL");
    }
}
