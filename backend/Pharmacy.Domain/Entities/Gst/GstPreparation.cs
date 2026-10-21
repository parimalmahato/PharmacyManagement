namespace Pharmacy.Domain.Entities.Gst;

// Tax annotations are separate from the original operational invoice/stock records.
/// <summary>
/// Stores versioned tax annotations separately from original operational invoice and return records.
/// </summary>
public sealed class GstDocumentDetails
{
    public long Id { get; set; }
    public string SourceKind { get; set; } = "";
    public long SourceId { get; set; }
    public string DetailsJson { get; set; } = "";
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// Tracks the branch and period worksheet revision, imported statement and review state independently of operational posting.
/// </summary>
public sealed class GstReturnWorkspace
{
    public long Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = "Draft";
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string? StatementJson { get; set; }
    public long? CurrentReviewId { get; set; }
}

/// <summary>
/// Preserves an internal accountant-review snapshot and source fingerprint; it is not evidence of GST return filing.
/// </summary>
public sealed class GstReturnReview
{
    public long Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Fingerprint { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public string ReviewedBy { get; set; } = "";
    public DateTime ReviewedAtUtc { get; set; }
}
