using Microsoft.AspNetCore.Identity;

namespace MySchoolDiary.Models
{
    public class User : IdentityUser
    {
        public string RefreshToken { get; set; }
        public DateTime TokenCreated { get; set; }
        public DateTime TokenExpires { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string FatherName { get; set; }
        public string? Form { get; set; }
    }
}
