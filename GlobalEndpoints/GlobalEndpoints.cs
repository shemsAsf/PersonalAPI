namespace PersonalAPI.GlobalEndpoints
{
    public static class GlobalEndpoints
    {
        public static void MapHelloEndpoint(this WebApplication app)
        {
            app.MapGet("/", () => Results.Ok(
                new
                {
                    project = "Global",
                    message = "Hello from the whole Design API",
                    version = "1.0.0"
                }));
        }
    }
}
