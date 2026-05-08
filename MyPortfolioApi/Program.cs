using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PersonalApi.Auth;
using PersonalApi.Database;
using PersonalApi.Storage;
using PersonalAPI.GlobalEndpoints;
using PersonalAPI.Projects.EkaterinaPotapovaDesign;
using PersonalAPI.Projects.Portfolio;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var isDev = builder.Environment.IsDevelopment();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string>()
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DynamicCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var ekaterinaSettings = builder.Configuration
    .GetSection("Auth:Ekaterina").Get<JwtSettings>()!;

builder.Services.AddKeyedSingleton("ekaterina", new JwtService(ekaterinaSettings));

builder.Services.AddAuthentication()
    .AddJwtBearer(
    "ekaterina-scheme", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = ekaterinaSettings.JwtIssuer,
            ValidAudience = ekaterinaSettings.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(ekaterinaSettings.JwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>();

var r2Settings = builder.Configuration.GetSection("R2").Get<R2Settings>()!;

builder.Services.AddSingleton(r2Settings);
builder.Services.AddSingleton<R2Service>();

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console();

    if (!isDev)
    {
        var lokiToken = ctx.Configuration["GRAFANA_LOKI_TOKEN"];
        var lokiUser = ctx.Configuration["GRAFANA_LOKI_USER"];
        var lokiUrl = ctx.Configuration["GRAFANA_LOKI_URL"]
                      ?? "https://logs-prod-us-central1.grafana.net";

        if (!string.IsNullOrEmpty(lokiToken))
        {
            cfg.WriteTo.GrafanaLoki(
                lokiUrl,
                credentials: new LokiCredentials
                {
                    Login = lokiUser,
                    Password = lokiToken
                },
                labels: new[] { new LokiLabel { Key = "app", Value = "personal-api" } }
            );
        }
    }
});

if (!isDev)
{
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    var otlpToken = builder.Configuration["GRAFANA_OTLP_TOKEN"];

    if (!string.IsNullOrEmpty(otlpEndpoint) && !string.IsNullOrEmpty(otlpToken))
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(t => t
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("personal-api"))
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(o => {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Headers = $"Authorization=Basic {otlpToken}";
                }));
    }
}


var app = builder.Build();

app.UseCors("DynamicCorsPolicy");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Database.Migrate();
    db.Database.EnsureCreated();
    db.GenerateFirstAdmin(ekaterinaSettings);
    db.SaveChanges();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHelloEndpoint();
app.MapEkaterinaEndpoint(ekaterinaSettings);
app.MapPortfolioEndpoint();

app.UseHttpMetrics();

if (!isDev)
    app.MapMetrics(); 

app.Run();