using System.ComponentModel.DataAnnotations;

namespace PersonalApi.Database.EkaterinaDesign
{
    public class EkaterinaDesignChats
    {
        [Key]
        public int UserId { get; set; }
        public DateTime SentAt {  get; set; } = DateTime.UtcNow;
        public string Name { get; set; }
        public string Message { get; set; }
        public bool IsAnswer { get; set; }
    }
}