namespace Pharmacy.Application.Branches;

/// <summary>
/// Supplies the active branch used by persistence filters; callers must not substitute model-provided branch identifiers.
/// </summary>
public interface IBranchContext { int BranchId { get; } }
/// <summary>
/// A branch directory entry without branch stock or financial details.
/// </summary>
public sealed record BranchDto(int Id, string Code, string Name, bool IsActive);
/// <summary>
/// Administrator input identifying a new operating branch by code and display name.
/// </summary>
public sealed record CreateBranchRequest(string Code, string Name);
