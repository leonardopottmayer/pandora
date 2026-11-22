using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Files;
using Pottmayer.Pandora.Modules.Finances.Application.Assistant;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

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
        services.AddScoped<IAssistantFileQueue, FinancesFileQueue>();

        // What an administrator can switch off and grant in this module (see the Admin module).
        services.AddTarsFeatureFlagDefinitions(f => f
            .Add(FinancesFeatures.Finances)
            .Add(FinancesFeatures.Import, parent: FinancesFeatures.Finances));
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(FinancesPermissions.Module, feature: FinancesFeatures.Finances)
                .Resource(FinancesPermissions.Accounts.Resource).Crud()
                .Resource(FinancesPermissions.Cards.Resource).Crud()
                .Resource(FinancesPermissions.Statements.Resource).Read().Actions(FinancesPermissions.Actions.Close, FinancesPermissions.Actions.Pay)
                .Resource(FinancesPermissions.Transactions.Resource).Read().Create().Update().Actions(FinancesPermissions.Actions.Post, FinancesPermissions.Actions.Reverse, FinancesPermissions.Actions.Void)
                .Resource(FinancesPermissions.Pending.Resource).Read().Create().Update().Actions(FinancesPermissions.Actions.Approve, FinancesPermissions.Actions.Reject)
                .Resource(FinancesPermissions.Recurrences.Resource).Crud()
                .Resource(FinancesPermissions.Categories.Resource).Read().Create().Update()
                .Resource(FinancesPermissions.Tags.Resource).Crud().Actions(FinancesPermissions.Actions.Assign)
                .Resource(FinancesPermissions.Attachments.Resource).Crud()
                .Resource(FinancesPermissions.Imports.Resource, feature: FinancesFeatures.Import).Read().Actions(FinancesPermissions.Actions.Run)
                .Resource(FinancesPermissions.Audit.Resource).Read());

        return services;
    }
}
