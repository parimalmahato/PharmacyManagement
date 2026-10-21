using System.ComponentModel.DataAnnotations;
using Pharmacy.Application.Reports;

namespace Pharmacy.Application.Assistant;

/// <summary>
/// Indicates an unavailable or invalid AI-provider result that callers can report or handle with an explicit non-generated fallback.
/// </summary>
public sealed class AiUnavailableException(string message) : Exception(message);
/// <summary>
/// Separates application use cases from text/vision generation and embedding providers; callers must validate generated business data.
/// </summary>
public interface IAiModel
{
    /// <summary>
    /// Generates untrusted text using explicit instructions and context; JSON requests still require application validation, and images require a vision-capable model.
    /// </summary>
    Task<string> GenerateAsync(string system, string prompt, bool json, string? image, CancellationToken ct);
    /// <summary>
    /// Returns one numeric embedding per input text in the original order for document indexing or semantic search.
    /// </summary>
    Task<float[][]> EmbedAsync(string[] texts, CancellationToken ct);
}
/// <summary>
/// Indexes and searches approved guide content using embeddings and a vector store, without indexing operational customer records.
/// </summary>
public interface ISemanticKnowledge
{
    /// <summary>
    /// Builds the current approved-guide vector index and returns the number of chunks submitted; deployment services must already be configured.
    /// </summary>
    Task<int> IndexAsync(CancellationToken ct);
    /// <summary>
    /// Finds relevant approved help sections using semantic similarity rather than querying operational records.
    /// </summary>
    Task<IReadOnlyList<HelpSource>> SearchAsync(string question, CancellationToken ct);
}
/// <summary>
/// A branch batch quantity and expiry snapshot without purchase costs; an expiry result may include stock that cannot be sold.
/// </summary>
public sealed record InventoryFact(int MedicineId, string Medicine, string Batch, DateOnly Expiry, int Quantity);
/// <summary>
/// Inputs for one medicine&apos;s reorder calculation: unexpired stock, stock expiring within coverage, net sold units and eligible draft supply.
/// </summary>
public sealed record DemandFact(int MedicineId, string Medicine, int Available, int Expiring, int Sold, int PendingPurchase);
/// <summary>
/// A transparent inventory-unit planning result; suggested quantities require staff review and do not create purchase orders.
/// </summary>
public sealed record ReorderSuggestion(int MedicineId, string Medicine, int Available, int Expiring, int Sold,
    int PendingPurchase, decimal DailyDemand, int TargetQuantity, int SuggestedQuantity);
/// <summary>
/// Exposes fixed read-only inventory and demand queries for the current branch plus the shared active medicine catalog.
/// </summary>
public interface IAiBusinessData
{
    /// <summary>
    /// Reads a supported inventory query for the current branch using explicit medicine/batch search and expiry-horizon controls, without returning purchase costs.
    /// </summary>
    Task<IReadOnlyList<InventoryFact>> InventoryAsync(string kind, string search, int days, CancellationToken ct);
    /// <summary>
    /// Reads branch reorder inputs for a bounded history and coverage horizon, separating stock from expected draft supply.
    /// </summary>
    Task<IReadOnlyList<DemandFact>> DemandAsync(int historyDays, int horizonDays, CancellationToken ct);
    /// <summary>
    /// Returns identifiers and names from the shared active medicine catalog for reviewable extraction suggestions.
    /// </summary>
    Task<IReadOnlyList<(int Id, string Name)>> MedicinesAsync(CancellationToken ct);
}
/// <summary>
/// A question that selects a supported inventory query; explicit search and day controls supply filters rather than model-generated SQL.
/// </summary>
public sealed class InventoryQuestion
{
    /// <summary>
    /// Bounded text used to choose stock, expiry or low-stock interpretation; it does not supply arbitrary SQL or branch identifiers.
    /// </summary>
    [Required, StringLength(1000)] public string Question { get; set; } = "";
    /// <summary>
    /// Explicit medicine-name or batch search filter; names mentioned only in the question are not automatically applied here.
    /// </summary>
    [StringLength(150)] public string Search { get; set; } = "";
    /// <summary>
    /// Explicit expiry horizon in India business days, limited to 1–365 days.
    /// </summary>
    [Range(1, 365)] public int Days { get; set; } = 60;
}
/// <summary>
/// The interpreted inventory query, actual interpretation mode and bounded branch results with their operational limitations.
/// </summary>
public sealed record InventoryAnswer(string Interpretation, string Mode, IReadOnlyList<InventoryFact> Batches, string Notes);
/// <summary>
/// Unverified supplier-document text or base64 PNG/JPEG input; direct PDF parsing is not supported.
/// </summary>
public sealed class InvoiceExtractionRequest
{
    /// <summary>
    /// Supplier-document text up to 20,000 characters; source content is untrusted model input rather than operational instructions.
    /// </summary>
    [StringLength(20000)] public string Text { get; set; } = "";
    /// <summary>
    /// Optional raw base64 PNG/JPEG page image; the service verifies its signature and a maximum decoded size of 4 MB.
    /// </summary>
    [StringLength(5_600_000)] public string? ImageBase64 { get; set; }
}
/// <summary>
/// Unverified supplier header and item values extracted by a model; missing facts remain nullable and must be checked against the source.
/// </summary>
public sealed record ExtractedInvoice(string? SupplierName, string? InvoiceNumber, DateOnly? InvoiceDate, List<ExtractedInvoiceLine> Items);
/// <summary>
/// Unverified per-inventory-unit invoice values; a missing or ambiguous date, price or quantity is represented as null.
/// </summary>
public sealed record ExtractedInvoiceLine(string? MedicineName, string? BatchNumber, DateOnly? ExpiryDate, int? Quantity,
    decimal? PurchasePrice, decimal? SellingPrice);
/// <summary>
/// An extracted line with an optional unique exact-name catalog suggestion; the suggestion is not proof of medicine identity.
/// </summary>
public sealed record ReviewedInvoiceLine(ExtractedInvoiceLine Extracted, int? SuggestedMedicineId);
/// <summary>
/// An unsaved extraction preview with catalog suggestions and warnings for mandatory staff review before normal purchase creation.
/// </summary>
public sealed record InvoiceExtraction(ExtractedInvoice Invoice, IReadOnlyList<ReviewedInvoiceLine> Lines, IReadOnlyList<string> Warnings);
/// <summary>
/// An advisory service-feedback classification, summary and themes; the result does not establish clinical facts or employee performance.
/// </summary>
public sealed record SentimentResult(string Sentiment, string Summary, string[] Themes);
/// <summary>
/// Existing report facts displayed alongside an optional generated explanation so users can compare narrative claims with verified totals.
/// </summary>
public sealed record SalesInsight(SalesReportDto Facts, string Explanation, string Mode);

/// <summary>
/// Calculates coverage-based purchase suggestions using recorded demand and stock; it is a deterministic rule rather than a trained forecast.
/// </summary>
public static class ReorderPlanner
{
    /// <summary>
    /// Computes a rounded-up demand coverage target, subtracts non-expiring usable stock and eligible draft supply, and clamps the suggestion to zero; this is not a learned forecast.
    /// </summary>
    /// <param name="fact">Recorded quantities for one medicine, all expressed in the same inventory unit.</param>
    /// <param name="historyDays">History length of 7–365 days used to calculate average daily net demand.</param>
    /// <param name="horizonDays">Target coverage of 1–180 days.</param>
    /// <returns>An advisory quantity requiring review of lead times, pack sizes and expected draft deliveries.</returns>
    /// <exception cref="ArgumentException">Either planning period is outside the supported bounds.</exception>
    public static ReorderSuggestion Calculate(DemandFact fact, int historyDays, int horizonDays)
    {
        if (historyDays is < 7 or > 365 || horizonDays is < 1 or > 180) throw new ArgumentException("Invalid planning period.");
        var demand = Math.Max(0, fact.Sold) / (decimal)historyDays;
        var target = checked((int)Math.Ceiling(demand * horizonDays));
        // Conservatively exclude stock expiring within the planning horizon. Draft orders are not sellable stock.
        var usable = Math.Max(0, fact.Available - fact.Expiring);
        var suggested = Math.Max(0, target - usable - Math.Max(0, fact.PendingPurchase));
        return new(fact.MedicineId, fact.Medicine, fact.Available, fact.Expiring, fact.Sold, fact.PendingPurchase,
            Math.Round(demand, 3), target, suggested);
    }
}
