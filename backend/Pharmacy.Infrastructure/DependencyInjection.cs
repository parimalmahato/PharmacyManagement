using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Medicines;
using Pharmacy.Infrastructure.Medicines;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Application.Inventory;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Application.Purchasing;
using Pharmacy.Infrastructure.Purchasing;
using Pharmacy.Application.Sales;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Application.Customers;
using Pharmacy.Infrastructure.Customers;
using Pharmacy.Application.Reports;
using Pharmacy.Infrastructure.Reports;
using Pharmacy.Infrastructure.Notifications;
using Pharmacy.Application.Notifications;
using Pharmacy.Infrastructure.Identity;

namespace Pharmacy.Infrastructure;

/// <summary>
/// Connects application contracts to SQL Server, identity, auditing and external AI adapters through dependency injection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers branch-aware SQL repositories, identity/audit services and bounded HTTP clients for external AI providers.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("PharmacyDb")
            ?? throw new InvalidOperationException(
                "PharmacyDb connection string is missing.");

        services.AddDbContext<PharmacyDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IMedicineRepository, MedicineRepository>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<Pharmacy.Application.Reports.IGstPreparationRepository, Pharmacy.Infrastructure.Reports.GstPreparationRepository>();
        services.AddScoped<Pharmacy.Application.Returns.ISaleReturnRepository, SaleReturnRepository>();
        services.AddScoped<Pharmacy.Application.Returns.IReturnInspectionContext, ReturnInspectionContext>();
        services.AddScoped<Pharmacy.Application.Payments.IPaymentRepository, Pharmacy.Infrastructure.Payments.PaymentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IAdvancedReportRepository, AdvancedReportRepository>();
        services.AddScoped<IReportExporter, ReportExporter>();
        services.AddScoped<Pharmacy.Application.Settings.IPharmacySettingsRepository, Pharmacy.Infrastructure.Settings.PharmacySettingsRepository>();
        services.AddScoped<ExpiryNotificationProcessor>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<JwtTokenService>();
        services.Configure<Pharmacy.Infrastructure.Assistant.HelpAssistantOptions>(configuration.GetSection("HelpAssistant"));
        services.Configure<Pharmacy.Infrastructure.Assistant.AiOptions>(configuration.GetSection("Ai"));
        services.AddSingleton<Pharmacy.Infrastructure.Assistant.GuideKnowledge>();
        services.AddScoped<Pharmacy.Application.Assistant.IHelpKnowledge, Pharmacy.Infrastructure.Assistant.HybridKnowledge>();
        services.AddScoped<Pharmacy.Application.Assistant.IAiBusinessData, Pharmacy.Infrastructure.Assistant.AiBusinessData>();
        services.AddHttpClient<Pharmacy.Application.Assistant.IAiModel, Pharmacy.Infrastructure.Assistant.OllamaAiModel>(client => client.Timeout = TimeSpan.FromSeconds(100))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<Pharmacy.Application.Assistant.ISemanticKnowledge, Pharmacy.Infrastructure.Assistant.SemanticKnowledge>(client => client.Timeout = TimeSpan.FromSeconds(100))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<Pharmacy.Application.Assistant.IHelpGenerator, Pharmacy.Infrastructure.Assistant.HelpGeneratorRouter>();
        services.AddHttpClient<Pharmacy.Infrastructure.Assistant.LangflowHelpGenerator>(client =>
            client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<BranchContext>();
        services.AddScoped<Pharmacy.Application.Branches.IBranchContext>(provider => provider.GetRequiredService<BranchContext>());
        return services;
    }
}
