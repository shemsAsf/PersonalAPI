using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace PersonalApi.Database.EkaterinaDesign
{
    public class EkaterinaDesignAdminLogins
    {
        [Key]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
    }
}