using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PersonalApi.Auth;
using PersonalApi.Database;
using PersonalApi.Storage;
using PersonalAPI.GlobalEndpoints;
using PersonalAPI.Projects.EkaterinaPotapovaDesign;
using PersonalAPI.Projects.Portfolio;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

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

app.Run();
