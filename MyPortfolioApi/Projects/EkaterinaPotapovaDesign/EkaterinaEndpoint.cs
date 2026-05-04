using PersonalApi.Auth;
using PersonalApi.Projects.EkaterinaPotapovaDesign.Features;

namespace PersonalAPI.Projects.EkaterinaPotapovaDesign
{
    public static class EkaterinaEndpoint
    {
        private const string Prefix = "/ekaterina";
        private const string Scheme = "ekaterina-scheme";

        public static void MapEkaterinaEndpoint(this WebApplication app, JwtSettings jwtSettings)
        {
            var group = app.MapGroup(Prefix);

            StatusFeature.Map(group);
            ProjectsFeature.Map(group, Scheme);
            AdminFeature.Map(group, jwtSettings, Scheme);
        }
    }
}
