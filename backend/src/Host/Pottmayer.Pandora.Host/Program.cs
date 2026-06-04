using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Host;
using Pottmayer.Pandora.Host.Localization;
using Pottmayer.Pandora.Modules.Identity.Application.DI;
using Pottmayer.Pandora.Modules.Identity.Infrastructure.DI;
using Pottmayer.Pandora.Modules.Identity.Persistence.DI;
using Pottmayer.Pandora.Modules.Identity.Presentation.DI;
using Pottmayer.Pandora.Shared.Infrastructure.DI;
using Pottmayer.Pandora.Shared.Persistence.DI;
using Pottmayer.Tars.Core.Localization.DI;
using Pottmayer.Tars.UserContext.AspNetCore;
using Pottmayer.Tars.Web.Http.AspNetCore.DI;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Shared
builder.AddPandoraSharedInfrastructure();
builder.AddPandoraSharedPersistence();

// Modules
builder.Services.AddIdentityPersistence();
builder.AddIdentityInfrastructure();
builder.Services.AddIdentityApplication();

// Web HTTP
builder.Services.AddTarsLocalization();
builder.Services.AddPandoraLocalization();
builder.Services.AddTarsProblemDetails();

// Presentation
builder.Services.AddControllers()
                .AddIdentityPresentationPart();

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
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
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
app.UseHttpsRedirection();
app.UseCors("PandoraClient");

app.UseAuthentication();
app.UseAuthorization();
app.UseTarsUserContext();

// Controllers
app.MapControllers();

app.Run();

public partial class Program { }
