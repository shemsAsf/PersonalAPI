using Microsoft.Extensions.Options;
using PersonalApi.Auth;
using System.Security.Cryptography;
using System.Text;

namespace PersonalApi.Projects.EkaterinaPotapovaDesign.Features
{
    public class AdminFeature
    {
        // Valid BCrypt hash of a random string, used so a wrong username
        // costs the same time as a wrong password (no user enumeration).
        private const string DummyHash =
            "$2a$11$C6UzMDM.H6dfI/f/IKcEeO5nY8hZ4v3d0mS1p0e0Qy9sQ9Zr7m8yK";

        public static void Map(RouteGroupBuilder group, JwtSettings jwtSettings, string scheme)
        {
            group.MapPost(
                "/login", (
                    LoginRequest req,
                    IOptions<EkaterinaAdminOptions> options,
                    [FromKeyedServices("ekaterina")] JwtService jwtService) =>
                {
                    var admin = options.Value;

                    var usernameMatch = CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(req.Username ?? string.Empty),
                        Encoding.UTF8.GetBytes(admin.Username));

                    var hash = string.IsNullOrEmpty(admin.PasswordHash)
                        ? DummyHash
                        : admin.PasswordHash;

                    var passwordMatch = BCrypt.Net.BCrypt.Verify(req.Password ?? string.Empty, hash);

                    if (!usernameMatch || !passwordMatch || string.IsNullOrEmpty(admin.PasswordHash))
                        return Results.Unauthorized();

                    var token = jwtService.GenerateToken(admin.Username, "ekaterina");
                    return Results.Ok(new { token });
                });

            group.MapGet("/verify", () => Results.Ok(new { message = "Successfully logged in" }))
                .RequireAuthorization(policy => policy
                    .AddAuthenticationSchemes(scheme)
                    .RequireAuthenticatedUser());
        }

        public record LoginRequest(string Username, string Password);
    }

    public class EkaterinaAdminOptions
    {
        public const string SectionName = "EkaterinaAdmin";

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}