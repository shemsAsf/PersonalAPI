namespace PersonalApi.Auth
{
    public class JwtSettings
    {
        public required string JwtSecret { get; init; }
        public required string JwtIssuer { get; init; }
        public required string JwtAudience { get; init; }
        public required string AdminUsername { get; init; }
        public required string AdminPasswordHash { get; init; }
    }
}
