using Microsoft.EntityFrameworkCore;
using PersonalApi.Auth;
using PersonalApi.Database;
using System.Security.Cryptography;
using System.Text;

namespace PersonalAPI.Projects.EkaterinaPotapovaDesign
{
    public static class EkaterinaEndpoint
    {
        private const string Prefix = "/ekaterina";
        private const string Scheme = "ekaterina-scheme";

        public static void MapEkaterinaEndpoint(this WebApplication app, JwtSettings jwtSettings)
        {
            var group = app.MapGroup(Prefix);

            group.MapGet("", () => Results.Ok(new
            {
                project = "EkaterinaPotapova.Design",
                message = "Hello from Ekaterina Potapova Design API",
                version = "1.0.0"
            }));


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
