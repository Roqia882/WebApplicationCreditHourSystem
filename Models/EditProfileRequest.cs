namespace WebApplication_Credit_hour_system.Models
{
    public class EditProfileRequest
    {

       
        public string? FullName { get; set; }
        public string? NameEn { get; set; }
        public string? NameAr { get; set; }
        public string Email { get; set; }
       // public string Level { get; set; }
        public string Year { get; set; }
        public string Semester { get; set; }
        public string? Phone { get; set; }
        public string? MajorEn { get; set; }
        public string? MajorAr { get; set; }
        public IFormFile? ProfilePicture { get; set; }
    }
}


