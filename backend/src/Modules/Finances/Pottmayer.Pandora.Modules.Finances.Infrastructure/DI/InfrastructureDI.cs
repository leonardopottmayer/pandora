using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Jobs;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddFinancesInfrastructure(this IHostApplicationBuilder builder)
    {
        // Windows-1252 / Latin1 code pages used by bank OFX/CSV exports (Itaú, Viacredi, Inter) —
        // not included by default outside the legacy code pages encoding provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        builder.Services.AddHostedService<StatementLifecycleBackgroundService>();
        builder.Services.AddHostedService<RecurrenceGenerationBackgroundService>();
        builder.Services.AddHostedService<ImportParsingBackgroundService>();

        builder.Services.AddSingleton<ILayoutDetector, LayoutDetector>();
        builder.Services.AddSingleton<IImportParser, OFXParser>();
        builder.Services.AddSingleton<IImportParser, CsvParser>();
        builder.Services.AddSingleton<IDuplicateDetector, DuplicateDetector>();

        return builder;
    }
}
