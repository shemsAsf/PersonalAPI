using Microsoft.EntityFrameworkCore;
using PersonalApi.Auth;
using PersonalApi.Database.EkaterinaDesign;
using System.Reflection.Emit;

namespace PersonalApi.Database
{
    public class AppDbContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public AppDbContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(Configuration.GetConnectionString("Database"));
        }
        
        public DbSet<EkaterinaDesignChats> EkaterinaDesignChat { get; set; }
        public DbSet<EkaterinaDesignAdminLogins> EkaterinaDesignAdminLogin { get; set; }

        public void GenerateFirstAdmin(JwtSettings jwtSettings) {

            if (EkaterinaDesignAdminLogin.Any())
                return;

            EkaterinaDesignAdminLogin.Add(new EkaterinaDesignAdminLogins
            {
                Username = jwtSettings.AdminUsername,
                Password = jwtSettings.AdminPasswordHash
            });
        }
    }
}
