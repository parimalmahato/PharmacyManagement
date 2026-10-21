namespace Pharmacy.Application.Returns;

/// <summary>
/// Provides the authenticated operator identity and eligibility required for explicit return-inspection approval.
/// </summary>
public interface IReturnInspectionContext
{
    string? ApproverId { get; }
}
