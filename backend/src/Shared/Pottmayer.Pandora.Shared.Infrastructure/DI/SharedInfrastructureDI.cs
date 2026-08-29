using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Messaging.DI;
using Pottmayer.Tars.Observability.AspNetCore.DI;
using Pottmayer.Tars.Observability.DI;
using Pottmayer.Tars.Observability.Options;
using Pottmayer.Tars.UserContext.AspNetCore.DI;
using Pottmayer.Tars.UserContext.DI;

namespace Pottmayer.Pandora.Shared.Infrastructure.DI;

public static class SharedInfrastructureDI
{
    public static IHostApplicationBuilder AddPandoraSharedInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddTarsInProcessIntegrationEventBus();
        builder.Services.AddUserContext();
        builder.AddObservability();

        return builder;
    }

    /// <summary>
    /// OpenTelemetry traces + metrics and native <c>ILogger</c> logging, all over OTLP. Gated by
    /// <c>Tars:Observability:Enabled</c> so a host without a collector can turn it off. The
    /// correlation-id middleware is wired separately in the pipeline via <c>UseTarsCorrelationId</c>.
    /// </summary>
    private static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        builder.AddTarsObservabilityOptions();

        var options = builder.Configuration
            .GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        if (!options.Enabled)
            return builder;

        var serviceName = string.IsNullOrWhiteSpace(options.ServiceName)
            ? builder.Environment.ApplicationName
            : options.ServiceName;

        builder.Services.AddTarsObservabilityResource(serviceName, options.ServiceVersion);

        builder.Services.AddTarsTracing();
        builder.Services.AddTarsAspNetCoreTracing();
        builder.Services.AddTarsHttpClientTracing();
        builder.Services.AddTarsTracingOtlpExporter(options.OtlpEndpoint);

        builder.Services.AddTarsMetrics();
        builder.Services.AddTarsAspNetCoreMetrics();
        builder.Services.AddTarsHttpClientMetrics();
        builder.Services.AddTarsRuntimeMetrics();
        builder.Services.AddTarsMetricsOtlpExporter(options.OtlpEndpoint);

        builder.Services.AddTarsLogging();
        builder.Services.AddTarsLoggingOtlpExporter(options.OtlpEndpoint);

        return builder;
    }

    private static IServiceCollection AddUserContext(this IServiceCollection services)
    {
        services.AddTarsUserContextAccessor();
        services.AddTarsCurrentPrincipalAccessor();
        services.AddTarsClaimsUserResolver<UserData>();
        services.AddTarsDefaultUserContextFactory<UserData>();
        services.AddTarsUserContextAccessor<UserData>();

        return services;
    }
}
