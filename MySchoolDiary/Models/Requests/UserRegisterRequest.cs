using System.ComponentModel.DataAnnotations;

namespace MySchoolDiary.Models.Requests
{
    public class UserRegisterRequest
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Password { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Surname { get; set; }
        [Required]
        public string FatherName { get; set; }
        public string? Form { get; set; }
    }
}
