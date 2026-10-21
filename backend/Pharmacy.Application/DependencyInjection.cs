using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Inventory;
using Pharmacy.Application.Medicines;
using Pharmacy.Application.Reports;
namespace Pharmacy.Application;

/// <summary>
/// Registers application use cases independently of database and external-provider implementations.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers application services while keeping storage, identity and provider implementations outside the application layer.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IMedicineService, MedicineService>();
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAdvancedReportService, AdvancedReportService>();
        services.AddScoped<GstPreparationService>();
        services.AddScoped<Pharmacy.Application.Assistant.HelpAssistant>();
        services.AddScoped<Pharmacy.Application.Assistant.AiFeatureService>();
        services.AddScoped<Pharmacy.Application.Payments.IUpiPaymentRequestService, Pharmacy.Application.Payments.UpiPaymentRequestService>();

        return services;
    }
}
