using Microsoft.EntityFrameworkCore;
using PersonalApi.Auth;
using PersonalApi.Database;

namespace PersonalApi.Projects.EkaterinaPotapovaDesign.Features
{
    public class AdminFeature
    {
        public static void Map(RouteGroupBuilder group, JwtSettings jwtSettings, string scheme)
        {
            group.MapPost(
                "/login", async (
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

            group.MapGet("/verify", () => Results.Ok(
                new
                {
                    message = "Successfully logged in"
                }
                )).RequireAuthorization(policy => policy
                    .AddAuthenticationSchemes(scheme)
                    .RequireAuthenticatedUser());
        }

        public record LoginRequest(string Username, string Password);
    }
}
