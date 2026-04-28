using Microsoft.EntityFrameworkCore;
using PersonalApi.Auth;
using PersonalApi.Database;
using PersonalApi.Projects.EkaterinaPotapovaDesign;
using PersonalApi.Storage;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonalAPI.Projects.EkaterinaPotapovaDesign
{
    public static class EkaterinaEndpoint
    {
        private const string Prefix = "/ekaterina";
        private const string Scheme = "ekaterina-scheme";
        private const string ProjectBucketPath = "ekaterinaDesign";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static void MapEkaterinaEndpoint(this WebApplication app, JwtSettings jwtSettings)
        {
            var group = app.MapGroup(Prefix);

            group.MapGet("", () => Results.Ok(new
            {
                project = "EkaterinaPotapova.Design",
                message = "Hello from Ekaterina Potapova Design API",
                version = "1.0.0"
            }));

            group.MapGet("/projects", async (R2Service r2) =>
            {
                var json = await r2.GetTextAsync(ProjectBucketPath + "/projects/index.json");

                if (json is null)
                    return Results.NotFound(new { message = "Project index not found" });

                var index = JsonSerializer.Deserialize<ProjectIndex>(json, JsonOptions);

                if (index is null)
                    return Results.Problem("Failed to parse project index");

                var projects = index.Projects
                    .Where(p => p.Visible)
                    .OrderByDescending(p => p.Id)
                    .Select(p => new ProjectSummary(
                        Id: p.Id,
                        Title: p.Title,
                        Subtitle: p.Subtitle,
                        Cover: r2.GetPublicUrl(p.Cover),
                        Tools: p.Tools.Select(r2.GetPublicUrl).ToList(),
                        Visible: p.Visible
                    ))
                    .ToList();

                return Results.Ok(new { projects });
            });


            #region Admin Login

            group.MapPost(
                "/login", async(
                    LoginRequest req,
                    AppDbContext dbContext,
                    [FromKeyedServicesAttribute("ekaterina")] JwtService jwtService) =>
            {
                var admin = await dbContext.EkaterinaDesignAdminLogin
                    .SingleOrDefaultAsync(a => a.Username == req.Username);

                var passwordMatch = BCrypt.Net.BCrypt.Verify(
                    req.Password,
                    admin?.Password);

                if (admin is null || !passwordMatch)
                    return Results.Unauthorized();
                
                var token = jwtService.GenerateToken(req.Username, "ekaterina");
                return Results.Ok(new { token });
            });

            group.MapGet("/admin", () => Results.Ok(
                new
                {
                    message = "Successfully logged in"
                }
                )).RequireAuthorization(policy => policy
                    .AddAuthenticationSchemes(Scheme)
                    .RequireAuthenticatedUser());

            #endregion Admin Login
        }

        public record LoginRequest(string Username, string Password);
    }
}
