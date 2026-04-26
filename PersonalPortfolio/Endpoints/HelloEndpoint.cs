namespace PersonalPortfolio.Endpoints
{
    public static class HelloEndpoint
    {
        public static void MapHelloEndpoint(this WebApplication app)
        {
            app.MapGet("/", () => Results.Ok(new
            {
                project = "Personal Portfolio",
                message = "Hello from Ekaterina Potapova Design API",
                version = "1.0.0"
            }));
        }
    }
}
