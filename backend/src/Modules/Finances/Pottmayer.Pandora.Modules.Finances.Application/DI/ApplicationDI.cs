using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Assistant;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Tars.Core.Mediator.DI;

namespace Pottmayer.Pandora.Modules.Finances.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddFinancesApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));
        services.AddScoped<IStatementResolver, StatementResolver>();

        // The Finances contribution to the assistant tool catalog.
        services.AddScoped<IAssistantTool, RecordExpenseTool>();
        services.AddScoped<IAssistantTool, ListTransactionsTool>();
        services.AddScoped<IAssistantTool, SummarizeTransactionsTool>();
        services.AddScoped<IAssistantTool, AccountBalancesTool>();
        services.AddScoped<IAssistantTool, ListCardsTool>();
        services.AddScoped<IAssistantTool, ListInboxTool>();

        return services;
    }
}
