namespace PersonalApi.Projects.EkaterinaPotapovaDesign.Features
{
    public class StatusFeature
    {
        public static void Map(RouteGroupBuilder group)
        {
            group.MapGet("", () => Results.Ok(new
            {
                project = "EkaterinaPotapova.Design",
                message = "Hello from Ekaterina Potapova Design API",
                version = "1.0.0"
            }));
        }
    }
}
