
namespace Pharmacy.Application.Authorization;

/// <summary>
/// Defines named authorization policies whose permitted roles are configured by the API host.
/// </summary>
public static class ApplicationPolicies
{
    public const string ManageMedicines = "ManageMedicines";
    public const string ViewMedicines = "ViewMedicines";

    public const string ManageInventory = "ManageInventory";
    public const string ViewInventory = "ViewInventory";

    public const string ManagePurchasing = "ManagePurchasing";

    public const string CreateSales = "CreateSales";
    public const string ViewSales = "ViewSales";

    public const string ManageCustomers = "ManageCustomers";

    public const string ViewReports = "ViewReports";
    public const string ViewExpiry = "ViewExpiry";
    public const string ManageNotifications = "ManageNotifications";

    public const string ManageUsers = "ManageUsers";
    public const string ViewCustomers = "ViewCustomers";
    public const string ManagePharmacySettings =
    "ManagePharmacySettings";
}
