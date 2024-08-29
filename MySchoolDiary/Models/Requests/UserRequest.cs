using System.ComponentModel.DataAnnotations;

namespace MySchoolDiary.Models.Requests
{
    public class UserRequest
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Password { get; set; }
    }
}
