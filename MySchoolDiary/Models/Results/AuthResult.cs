namespace MySchoolDiary.Models.Results
{
    public class AuthResult
    {
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public bool Successfully { get; set; }
        public List<string> Errors { get; set; }
    }
}
