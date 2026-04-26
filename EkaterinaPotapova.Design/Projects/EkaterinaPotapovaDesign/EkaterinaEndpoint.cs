namespace PersonalAPI.Projects.EkaterinaPotapovaDesign
{
    public static class EkaterinaEndpoint
    {
        private const string Prefix = "/ekaterina";

        public static void MapEkaterinaEndpoint(this WebApplication app)
        {
            var group = app.MapGroup(Prefix);

            group.MapGet("", () => Results.Ok(new
            {
                project = "EkaterinaPotapova.Design",
                message = "Hello from Ekaterina Potapova Design API",
                version = "1.0.0"
            }));
        }

    }

}
