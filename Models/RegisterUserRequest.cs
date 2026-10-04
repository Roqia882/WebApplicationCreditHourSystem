namespace WebApplication_Credit_hour_system.Models
{
    public class RegisterUserRequest
    {
      
            public string Name { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public int StudentID { get; set; }
            public int Year { get; set; }
            public string Language { get; set; } // "en" أو "ar"
        }
    }


