namespace EkaterinaPotapova.Design.Endpoints
{
    public static class HelloEndpoint
    {
        public static void MapHelloEndpoint(this WebApplication app)
        {
            app.MapGet("/", () => Results.Ok(new
            {
                project = "EkaterinaPotapova.Design",
                message = "Hello from Ekaterina Potapova Design API",
                version = "1.0.0"
            }));
        }
    }
}
