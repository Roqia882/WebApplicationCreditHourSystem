using System.Collections.Generic;
namespace WebApplication_Credit_hour_system.Models
{
    public class StudentProfile
    {
       
            public int StudentID { get; set; }
            public string Name { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
            public string Major { get; set; }
       
        
        public string Year { get; set; }
        public string Semester { get; set; }
        public decimal GPA { get; set; } // أو decimal?
        public int? CompletedCreditHours { get; set; }
           
            public string Phone { get; set; }

        public string ProfilePictureUrl { get; set; }
    }
    }



