namespace PersonalAPI.Projects.Portfolio
{
    public static class PortfolioEndpoint
    {
        private const string Prefix = "/portfolio";

        public static void MapPortfolioEndpoint(this WebApplication app)
        {
            var group = app.MapGroup(Prefix);

            group.MapGet("", () => Results.Ok(new
            {
                project = "PersonalWebsite",
                message = "Hello from Personal Website Design API",
                version = "1.0.0"
            }));
        }
    }
}
