using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

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
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.GrafanaLoki(
           "https://logs-prod-us-central1.grafana.net",
           credentials: new LokiCredentials
           {
               Login = "your-loki-user-id",
               Password = ctx.Configuration["GRAFANA_LOKI_TOKEN"]
           },
           labels: new[] { new LokiLabel { Key = "app", Value = "your-api" } }
       ));

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("your-api"))
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(o => {
            o.Endpoint = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]!);
            o.Headers = $"Authorization=Basic {builder.Configuration["GRAFANA_OTLP_TOKEN"]}";
        }));

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
app.MapMetrics();

app.Run();
