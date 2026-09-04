using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Host;
using Pottmayer.Pandora.Host.Localization;
using Pottmayer.Pandora.Modules.Identity.Application.DI;
using Pottmayer.Pandora.Modules.Identity.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Identity.Persistence.DI;
using Pottmayer.Pandora.Modules.Identity.Presentation.DI;
using Pottmayer.Pandora.Modules.Channels.Application.DI;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Channels.Persistence.DI;
using Pottmayer.Pandora.Modules.Channels.Presentation.DI;
using Pottmayer.Pandora.Modules.Finances.Application.DI;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Finances.Persistence.DI;
using Pottmayer.Pandora.Modules.Finances.Presentation.DI;
using Pottmayer.Pandora.Modules.Notes.Application.DI;
using Pottmayer.Pandora.Modules.Notes.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Notes.Persistence.DI;
using Pottmayer.Pandora.Modules.Notes.Presentation.DI;
using Pottmayer.Pandora.Modules.Agenda.Application.DI;
using Pottmayer.Pandora.Modules.Agenda.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Agenda.Persistence.DI;
using Pottmayer.Pandora.Modules.Agenda.Presentation.DI;
using Pottmayer.Pandora.Modules.Integrations.Application.DI;
using Pottmayer.Pandora.Modules.Integrations.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Integrations.Persistence.DI;
using Pottmayer.Pandora.Modules.Integrations.Presentation.DI;
using Pottmayer.Pandora.Modules.Assistant.Application.DI;
using Pottmayer.Pandora.Modules.Assistant.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Assistant.Persistence.DI;
using Pottmayer.Pandora.Modules.Assistant.Presentation.DI;
using Pottmayer.Pandora.Shared.Infrastructure.DI;
using Pottmayer.Pandora.Shared.Persistence.DI;
using Pottmayer.Tars.Core.Localization.DI;
using Pottmayer.Tars.Observability.AspNetCore.DI;
using Pottmayer.Tars.UserContext.AspNetCore;
using Pottmayer.Tars.Web.Http.AspNetCore.DI;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Shared (registers observability too — see AddPandoraSharedInfrastructure)
builder.AddPandoraSharedInfrastructure();
builder.AddPandoraSharedPersistence();

// Modules
builder.Services.AddIdentityPersistence();
builder.AddIdentityInfrastructure();
builder.Services.AddIdentityApplication();

builder.Services.AddChannelsPersistence();
builder.AddChannelsInfrastructure();
builder.Services.AddChannelsApplication();

builder.Services.AddFinancesPersistence();
builder.AddFinancesInfrastructure();
builder.Services.AddFinancesApplication();

builder.Services.AddNotesPersistence();
builder.AddNotesInfrastructure();
builder.Services.AddNotesApplication();

builder.Services.AddAgendaPersistence();
builder.AddAgendaInfrastructure();
builder.Services.AddAgendaApplication();

builder.Services.AddIntegrationsPersistence();
builder.AddIntegrationsInfrastructure();
builder.Services.AddIntegrationsApplication();

builder.Services.AddAssistantPersistence();
builder.AddAssistantInfrastructure();
builder.Services.AddAssistantApplication();

// The monolith's messaging transport — the in-process transactional outbox, wired in one place.
// Registered after the modules so every contract assembly and database key is known.
builder.AddPandoraOutbox();

// Web HTTP
builder.Services.AddTarsLocalization();
builder.Services.AddPandoraLocalization();
builder.Services.AddFinancesLocalization();
builder.Services.AddTarsProblemDetails();

// Presentation
builder.Services.AddControllers()
                .AddIdentityPresentationPart()
                .AddChannelsPresentationPart()
                .AddFinancesPresentationPart()
                .AddNotesPresentationPart()
                .AddAgendaPresentationPart()
                .AddIntegrationsPresentationPart()
                .AddAssistantPresentationPart();

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();

// Forwarded headers (running behind a reverse proxy: nginx now, Cloudflare in phase 2).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The proxy is not on a fixed address inside the Docker network; trust it.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PandoraClient", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(_ => true);
        }
        else
        {
            var origins = builder.Configuration.GetSection("Pandora:Cors:AllowedOrigins").Get<string[]>();
            if (origins is { Length: > 0 })
                policy.WithOrigins(origins);
        }

        policy.AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Authorization");
    });
});

var app = builder.Build();

// Must run first so downstream middleware sees the real scheme/host.
app.UseForwardedHeaders();

// Correlation id early, so every downstream log and span inherits it.
if (builder.Configuration.GetValue("Tars:Observability:Enabled", true))
    app.UseTarsCorrelationId();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                $"Pandora API {description.GroupName.ToUpperInvariant()}");
        }
        options.RoutePrefix = string.Empty;
    });
}

// Localization
app.UseRequestLocalization(options =>
{
    var supported = new[] { "en", "pt-BR" };
    options.SetDefaultCulture("en")
           .AddSupportedCultures(supported)
           .AddSupportedUICultures(supported);
});

// Middleware
// HTTPS is terminated by the reverse proxy in container/prod; only redirect
// where Kestrel actually serves HTTPS (local dev).
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("PandoraClient");

app.UseAuthentication();
app.UseAuthorization();
app.UseTarsUserContext();

// Controllers
app.MapControllers();

app.Run();

public partial class Program { }
