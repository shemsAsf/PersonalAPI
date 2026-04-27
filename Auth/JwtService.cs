using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PersonalApi.Auth
{
    public class JwtService(JwtSettings settings)
    {
        public string GenerateToken(string username, string project)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim("project", project),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
            issuer: settings.JwtIssuer,
            audience: settings.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
        );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
